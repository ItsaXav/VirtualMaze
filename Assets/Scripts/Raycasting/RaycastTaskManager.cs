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
using System.IO;
using UnityEngine.SceneManagement;

namespace VirtualMaze.Assets.Scripts.Raycasting {

    public class RaycastTaskManager : MonoBehaviour {
        private Transform cameraRobot;
        private CueController cueController;

        public static readonly decimal ACCEPTED_TIME_DIFF = 20;

        public static readonly int FRAME_PER_BATCH = 100;

        public float PercentageCompleted {get; private set;}

        private EyeDataReader EyeReader;
        private SessionDataReader SessionReader;
        private BinMapper BinMapper;
        private RayCastRecorder Recorder;

        

        private RaycastTaskManager(EyeDataReader eyeDataReader, SessionDataReader sessionReader,
        BinMapper binMapper, RayCastRecorder recorder, Transform camRobot, CueController cueController) {
            this.EyeReader = eyeDataReader;
            this.SessionReader = sessionReader;
            this.BinMapper = binMapper;
            this.Recorder = recorder;
            this.cameraRobot = camRobot;
            this.cueController = cueController;
        }

        public static RaycastTaskManager GetManager(string sessionPath, 
            string eyeDataPath, string writePath, BinMapper binMapper, ScreenSaver screenSaver) {
            // H5.close();
            // H5.open();
            screenSaver.fadeController.gameObject.SetActive(false);
            screenSaver.CueBinCollider.SetActive(true);
            screenSaver.HintBinCollider.SetActive(true);

            EyeDataReader eyeReader = null;

            Physics.SyncTransforms();

            if (Path.GetExtension(eyeDataPath).Equals(".mat")) {
                try {
                    eyeReader = new EyeMatReader(eyeDataPath);
                }
                catch (Exception e) {
                    Debug.LogException(e);
                    Console.WriteError("Unable to open eye data mat file.");
                }
            }

            SessionDataReader sessionReader = new MatSessionReader(sessionPath);
            // assume it's just mat for now


            if (eyeReader == null || sessionReader == null) {
               throw new FileLoadException("Could not load file! Check extension");
            }

            string filename = $"{Path.GetFileNameWithoutExtension(sessionPath)}" +
                $"_{Path.GetFileNameWithoutExtension(eyeDataPath)}.csv";
            RayCastRecorder recorder = new RayCastRecorder(writePath, filename);

            // At this point, all essentials are available. Create the object.

            RaycastTaskManager raycastTaskManager = new RaycastTaskManager(eyeReader, 
                sessionReader, binMapper, recorder, screenSaver.robot, screenSaver.cueController);

           

            raycastTaskManager.cueController.SetMode(CueController.Mode.Recording);
            raycastTaskManager.cueController.UpdatePosition(raycastTaskManager.cameraRobot);
            Physics.SyncTransforms();

            SceneManager.LoadScene("Double Tee");

            DateTime start = DateTime.Now;
            Debug.LogError($"s: {start}");

            return raycastTaskManager;
            // Console.Write($"s: {start}, e: {DateTime.Now}");
            // Debug.LogError($"s: {start}, e: {DateTime.Now}");

            // /* Clean up */
            // //SceneManager.LoadScene("Start");
            // fadeController.gameObject.SetActive(true);
            // progressBar.gameObject.SetActive(false);
            // cueController.SetMode(CueController.Mode.Experiment);
            // CueBinCollider.SetActive(false);
            // HintBinCollider.SetActive(false);
        }
        public IEnumerator ProcessSession() {

        int frameCounter = 0;
        int trialCounter = 1;


        //Move to first Trial Trigger
        SessionReader.moveToNextTrigger(SessionTrigger.TrialStartedTrigger);
        EyeReader.moveToNextTrigger(SessionTrigger.TrialStartedTrigger);
        AllFloatData data = EyeReader.GetCurrentData();


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
        while (SessionReader.HasNext /*&& numberOfTriggers < 8*/) {
                numberOfTriggers++;
                /*add current to buffer since sessionData.timeDelta is the time difference from the previous frame.
                    * and the previous frame raised a trigger for it to be printed in this frame*/

                sessionFrames.Enqueue(SessionReader.CurrentData);

                List<SessionData> sessionDatas = 
                    SessionReader.GetUntilTrigger(SessionTrigger.NoTrigger);
                List<AllFloatData> eyeDatas =
                    EyeReader.GetUntilEvent(DataTypes.MESSAGEEVENT);

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
                            jobHandler.writeToFile(currData,Recorder,cameraRobot,isLastSampleInFrame);
                        }
                    }
                    //Profiler.EndSample();

                    //Profiler.BeginSample("MultiCast Processing");
                    // while (jobQueue.Count > 0) {
                    //     using (BinWallManager.BinGazeJobData job = jobQueue.Dequeue()) {
                    //         while (!job.h.IsCompleted) {

                    //         }
                    //         job.h.Complete();

                    //         job.process(BinMapper, binsHitId);

                    //         //Profiler.BeginSample("HDFwrite");
                    //         binRecorder.RecordMovement(job.sampleTime, binsHitId);
                    //         //Profiler.EndSample();

                    //         //Profiler.BeginSample("ClearHashset");
                    //         binsHitId.Clear();
                    //         //Profiler.EndSample();
                    //     }
                    // }
                    // //Profiler.EndSample();

                    binSamples.Clear();
                    if (currData is Fsample) {
                        binSamples.Add((Fsample)currData);
                    }
                    // else if (currData is MessageEvent) {
                    //     ProcessTrigger(currData);
                    // }

                    frameCounter %= FRAME_PER_BATCH;
                    if (frameCounter == 0) {
                        PercentageCompleted = SessionReader.ReadProgress;
                        yield return null;
                    }
                    //gazePointPool?.ClearScreen();
                }

                Debug.Log($"ses: {sessionFrames.Count}| fix: {fixations.Count}, timestamp {gazeTime:F4}, timepassed{timepassed:F4}");
                decimal finalExcess = gazeTime - timepassed;

                Debug.Log($"FINAL EXCESS: {finalExcess} | {SessionReader.CurrentData.timeDeltaMs}");
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