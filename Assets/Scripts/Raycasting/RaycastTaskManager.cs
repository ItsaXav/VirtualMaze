using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtualMaze.Assets.Scripts.Raycasting;

public class RaycastTaskManager
{
    
    private IEnumerator ProcessSession(
        ISessionDataReader sessionReader,
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

    List<Fsample> binSamples = new List<Fsample>();
    Queue<BinWallManager.BinGazeJobData> jobQueue = new Queue<BinWallManager.BinGazeJobData>();
    HashSet<int> binsHitId = new HashSet<int>();

    //feed in current Data due to preparation moving the data pointer forward
    fixations.Enqueue(data);

    int debugMaxMissedOffset = 0;

    List<Fsample> sampleCache = new List<Fsample>();
    int numberOfTriggers = 0;
    while (sessionReader.HasNext /*&& numberOfTriggers < 8*/) {
            numberOfTriggers++;
            /*add current to buffer since sessionData.timeDelta is the time difference from the previous frame.
                * and the previous frame raised a trigger for it to be printed in this frame*/

            sessionFrames.Enqueue(sessionReader.CurrentData);

            decimal excessTime = EnqueueData(sessionFrames, sessionReader, fixations, eyeReader, out int status, out string reason);

            decimal timepassed = fixations.Peek().time;
            decimal c1 = 0;
            decimal c2 = 0;

            decimal timeOffset = excessTime / (sessionFrames.Count - 1);

            print($"timeError: {excessTime}|{timeOffset} for {sessionFrames.Count} frames @ {sessionReader.CurrentIndex} and {fixations.Count} fix");
            uint gazeTime = 0;

            decimal debugtimeOffset = 0;

            while (sessionFrames.Count > 0 && fixations.Count > 0) {
                SessionData sessionData = sessionFrames.Dequeue();
                frameCounter++;

                decimal period;
                if (sessionFrames.Count > 0) {
                    //peek since next sessionData holds the time it takes from this data to the next
                    period = (sessionFrames.Peek().timeDeltaMs) - timeOffset;
                }
                else {
                    //use current data's timedelta to approximate since peeking at the next data's timedelta is not supported
                    period = (sessionData.timeDeltaMs) - timeOffset;
                }

                KahanSummation(ref debugtimeOffset, ref c2, timeOffset);
                KahanSummation(ref timepassed, ref c1, period);

                MoveRobotTo(robot, sessionData);

                BinWallManager.ResetWalls();

                List<Fsample> frameGazes = new List<Fsample>();

                bool isLastSampleInFrame = false;
                AllFloatData currData = null;

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

                Profiler.BeginSample("SyncTransform");
                Physics.SyncTransforms();
                Profiler.EndSample();

                /* process binsamples and let the raycast run in Jobs */
                Profiler.BeginSample("RaycastingSinglePrepare");
                RaycastGazesJob rCastJob = RaycastGazes(binSamples, recorder, currData, default);
                Profiler.EndSample();

                /* Start the binning process while rCastJob is running */
                Profiler.BeginSample("Binning");
                BinGazes(binSamples, binRecorder, jobQueue, mapper);
                Profiler.EndSample();

                Profiler.BeginSample("RaycastingSingleProcess");
                if (rCastJob != null) {
                    using (rCastJob) {
                        rCastJob.h.Complete(); //force completion if not done yet
                        rCastJob.Process(currData, recorder, robot, isLastSampleInFrame, gazePointPool, displayGazes: frameCounter == Frame_Per_Batch, GazeCanvas, viewport);
                    }
                }
                Profiler.EndSample();

                Profiler.BeginSample("MultiCast Processing");
                while (jobQueue.Count > 0) {
                    using (BinWallManager.BinGazeJobData job = jobQueue.Dequeue()) {
                        while (!job.h.IsCompleted) {

                        }
                        job.h.Complete();

                        job.process(mapper, binsHitId);

                        Profiler.BeginSample("HDFwrite");
                        binRecorder.RecordMovement(job.sampleTime, binsHitId);
                        Profiler.EndSample();

                        Profiler.BeginSample("ClearHashset");
                        binsHitId.Clear();
                        Profiler.EndSample();
                    }
                }
                Profiler.EndSample();

                binSamples.Clear();
                if (currData is Fsample) {
                    binSamples.Add((Fsample)currData);
                }
                else if (currData is MessageEvent) {
                    ProcessTrigger(currData);
                }

                frameCounter %= Frame_Per_Batch;
                if (frameCounter == 0) {
                    progressBar.value = sessionReader.ReadProgress;
                    yield return null;
                }
                gazePointPool?.ClearScreen();
            }

            Debug.Log($"ses: {sessionFrames.Count}| fix: {fixations.Count}, timestamp {gazeTime:F4}, timepassed{timepassed:F4}");
            decimal finalExcess = gazeTime - timepassed;

            Debug.Log($"FINAL EXCESS: {finalExcess} | {sessionReader.CurrentData.timeDeltaMs}");
            Debug.Log($"Frame End total time offset: {debugtimeOffset}");

            //clear queues to prepare for next trigger
            sessionFrames.Clear();

            if (Math.Abs(finalExcess) > Accepted_Time_Diff) {
                Debug.LogError($"Final Excess ({finalExcess}) Larger that Accepted time diff ({Accepted_Time_Diff})");
            }

            if (fixations.Count > 0) {
                Debug.LogWarning($"{fixations.Count} fixations assumed to belong to next trigger");
                while (fixations.Count > 0) {
                    debugMaxMissedOffset = Math.Max(fixations.Count, debugMaxMissedOffset);

                    if (ProcessData(fixations.Dequeue(), recorder, false, binRecorder) == SessionTrigger.TrialStartedTrigger) {
                        trialCounter++;
                        SessionStatusDisplay.DisplayTrialNumber(trialCounter);
                    }
                }
            }
        }

        Debug.LogError(debugMaxMissedOffset);

    }

   
}