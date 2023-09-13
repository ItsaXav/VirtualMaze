using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualMaze.Assets.Scripts.Misc;
using VirtualMaze.Assets.Scripts.DataReaders;
using Eyelink.Structs;
using UnityEngine;
using VirtualMaze.Assets.Scripts.Raycasting;
using Unity.Jobs;

namespace VirtualMaze.Assets.Scripts.Raycasting {

    public class RaycastTaskManager
    {
        public Transform cameraRobot;
        public CueController cueController;

        public static readonly decimal ACCEPTED_TIME_DIFF = 20;
        private IEnumerator ProcessSession(
            SessionDataReader sessionReader,
            EyeDataReader eyeReader,
            RayCastRecorder recorder,
            BinRecorder binRecorder,
            BinMapper mapper) {

        int frameCounter = 0;
        int trialCounter = 1;

        if ((sessionReader == null) || (eyeReader == null)) {
            yield break;
        }

        //Move to first Trial Trigger
        sessionReader.moveToNextTrigger(SessionTrigger.TrialStartedTrigger);
        eyeReader.moveToNextTrigger(SessionTrigger.TrialStartedTrigger);
        AllFloatData data = eyeReader.GetCurrentData();


        Queue<SessionData> sessionFrames = new Queue<SessionData>();
        Queue<AllFloatData> fixations = new Queue<AllFloatData>();
        fixations.Enqueue(data);

        List<Fsample> binSamples = new List<Fsample>();
        Queue<BinWallManager.BinGazeJobData> jobQueue = new Queue<BinWallManager.BinGazeJobData>();
        HashSet<int> binsHitId = new HashSet<int>();

        //feed in current Data due to preparation moving the data pointer forward

        int debugMaxMissedOffset = 0;

        List<Fsample> sampleCache = new List<Fsample>();


        int numberOfTriggers = 0;
        while (sessionReader.HasNext /*&& numberOfTriggers < 8*/) {
                numberOfTriggers++;
                /*add current to buffer since sessionData.timeDelta is the time difference from the previous frame.
                    * and the previous frame raised a trigger for it to be printed in this frame*/

                sessionFrames.Enqueue(sessionReader.CurrentData);

                List<SessionData> sessionDatas = 
                    sessionReader.GetUntilTrigger(SessionTrigger.NoTrigger);
                List<AllFloatData> eyeDatas =
                    eyeReader.GetUntilEvent(DataTypes.MESSAGEEVENT);

                decimal totalTriggerTime = 0;
                decimal ca = 0;
                foreach (SessionData dataPoint in sessionDatas) {

                    KahanSummation.Sum(ref totalTriggerTime, ref ca, dataPoint.timeDeltaMs);
                }

                decimal totalEyeTime = 0;
                decimal cb = 0;
                foreach(AllFloatData eyePoint in eyeDatas) {
                    KahanSummation.Sum(ref totalEyeTime, ref cb, eyePoint.time);
                }
                
                // the discrepancy in time between the two, should be 0 normally
                decimal excessTime = totalTriggerTime - totalEyeTime; 

                

                decimal timepassed = fixations.Peek().time;
                decimal c1 = 0;
                decimal c2 = 0;

                decimal timeOffset = excessTime / (sessionFrames.Count - 1);

                // print($"timeError: {excessTime}|{timeOffset} for {sessionFrames.Count} frames @ {sessionReader.CurrentIndex} and {fixations.Count} fix");
                uint gazeTime = 0;

                decimal debugtimeOffset = 0;

                while (sessionFrames.Count > 0 && fixations.Count > 0) {
                    SessionData curUnityData = sessionFrames.Dequeue();
                    frameCounter++;

                    decimal period;
                    if (sessionFrames.Count > 0) {
                        //peek since next sessionData holds the time it takes from this data to the next
                        period = (sessionFrames.Peek().timeDeltaMs) - timeOffset;
                    }
                    else {
                        //use current data's timedelta to approximate since peeking at the next data's timedelta is not supported
                        period = (curUnityData.timeDeltaMs) - timeOffset;
                    }

                    KahanSummation.Sum(ref debugtimeOffset, ref c2, timeOffset);
                    KahanSummation.Sum(ref timepassed, ref c1, period);
                    
                    RobotMovement.MoveRobotTo(cameraRobot, curUnityData.config);
                    cueController.UpdatePosition(cameraRobot);


                    BinWallManager.ResetWalls();

                    List<Fsample> frameGazes = new List<Fsample>();

                    bool isLastSampleInFrame = false;
                    AllFloatData currData = null;
                    // timepassed is the sum of periods
                    while (gazeTime <= timepassed && fixations.Count > 0) {
                        currData = fixations.Dequeue();
                        gazeTime = currData.time;

                        isLastSampleInFrame = gazeTime > timepassed;

                        if (currData is MessageEvent || isLastSampleInFrame) {
                            break;
                        }
                        else if (currData is Fsample) {
                            binSamples.Add((Fsample)currData);
                        }
                    }

                    //Profiler.BeginSample("SyncTransform");
                    Physics.SyncTransforms();
                    //Profiler.EndSample();

                    /* process binsamples and let the raycast run in Jobs */
                    //Profiler.BeginSample("RaycastingSinglePrepare");
                    RaycastJobHandler jobHandler = RaycastJobHandler.handleSamples(binSamples, currData, default, Camera.main);
                    //Profiler.EndSample();

                    /* Start the binning process while rCastJob is running */
                    //Profiler.BeginSample("Binning");
                    //BinGazes(binSamples, binRecorder, jobQueue, mapper);
                    //Profiler.EndSample();

                    //Profiler.BeginSample("RaycastingSingleProcess");
                    if (jobHandler != null) {
                        using (jobHandler) {
                            jobHandler.h.Complete(); //force completion if not done yet
                            jobHandler.writeToFile(currData,recorder,cameraRobot,isLastSampleInFrame);
                        }
                    }
                    //Profiler.EndSample();

                    //Profiler.BeginSample("MultiCast Processing");
                    while (jobQueue.Count > 0) {
                        using (BinWallManager.BinGazeJobData job = jobQueue.Dequeue()) {
                            while (!job.h.IsCompleted) {

                            }
                            job.h.Complete();

                            job.process(mapper, binsHitId);

                            //Profiler.BeginSample("HDFwrite");
                            binRecorder.RecordMovement(job.sampleTime, binsHitId);
                            //Profiler.EndSample();

                            //Profiler.BeginSample("ClearHashset");
                            binsHitId.Clear();
                            //Profiler.EndSample();
                        }
                    }
                    //Profiler.EndSample();

                    binSamples.Clear();
                    if (currData is Fsample) {
                        binSamples.Add((Fsample)currData);
                    }
                    // else if (currData is MessageEvent) {
                    //     ProcessTrigger(currData);
                    // }

                    // frameCounter %= Frame_Per_Batch;
                    // if (frameCounter == 0) {
                    //     progressBar.value = sessionReader.ReadProgress;
                    //     yield return null;
                    // }
                    //gazePointPool?.ClearScreen();
                }

                Debug.Log($"ses: {sessionFrames.Count}| fix: {fixations.Count}, timestamp {gazeTime:F4}, timepassed{timepassed:F4}");
                decimal finalExcess = gazeTime - timepassed;

                Debug.Log($"FINAL EXCESS: {finalExcess} | {sessionReader.CurrentData.timeDeltaMs}");
                Debug.Log($"Frame End total time offset: {debugtimeOffset}");

                //clear queues to prepare for next trigger
                sessionFrames.Clear();

                if (Math.Abs(finalExcess) > ACCEPTED_TIME_DIFF) {
                    Debug.LogError($"Final Excess ({finalExcess}) Larger that Accepted time diff ({ACCEPTED_TIME_DIFF})");
                }

                if (fixations.Count > 0) {
                    Debug.LogWarning($"{fixations.Count} fixations assumed to belong to next trigger");
                    while (fixations.Count > 0) {
                        debugMaxMissedOffset = Math.Max(fixations.Count, debugMaxMissedOffset);

                        // if (ProcessData(fixations.Dequeue(), recorder, false, binRecorder) == SessionTrigger.TrialStartedTrigger) {
                        //     trialCounter++;
                        //     SessionStatusDisplay.DisplayTrialNumber(trialCounter);
                        // }
                    }
                }
            }

            Debug.LogError(debugMaxMissedOffset);

        }

    
    }
}