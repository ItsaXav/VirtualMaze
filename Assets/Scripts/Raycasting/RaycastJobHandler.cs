using System;
using System.Collections.Generic;
using Eyelink.Structs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace VirtualMaze.Assets.Scripts.Raycasting {
    public class RaycastJobHandler : IDisposable {

        int numSamples;
        List<Fsample> binSamples;
        NativeArray<RaycastHit> results;
        NativeArray<RaycastCommand> cmds;
        public JobHandle h;

        private RaycastJobHandler(int numSamples, 
        List<Fsample> binSamples, 
        NativeArray<RaycastHit> results, 
        NativeArray<RaycastCommand> cmds, 
        JobHandle h) {
            this.numSamples = numSamples;
            this.binSamples = binSamples;
            this.results = results;
            this.cmds = cmds;
            this.h = h;
        }
        
        public static RaycastJobHandler handleSamples(List<Fsample> binSamples, 
        AllFloatData currData, 
        JobHandle dependancy, Camera raycasterCam) {
            
        int numCommands = binSamples.Count;
        // One command for each sample

        if (numCommands == 0) {
            // if (currData is MessageEvent) {
            //     recorder.FlagEvent(((MessageEvent)currData).message);
            // }
            return null;
        }

        NativeArray<RaycastCommand> cmds = new NativeArray<RaycastCommand>(numCommands, Allocator.TempJob);
        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(numCommands, Allocator.TempJob);


        for (int i = 0; i < numCommands; i++) {
            Vector2 origGaze = binSamples[i].RightGaze;
            Vector2 viewportGaze = 
                RangeCorrector.RangeCorrector.HD_TO_VIEWPORT.correctVector(origGaze);
            
            Ray r = raycasterCam.ViewportPointToRay(viewportGaze);
            
            cmds[i] = new RaycastCommand(r.origin,
                r.direction, layerMask: BinWallManager.ignoreBinningLayer);
            
        }

        JobHandle h = RaycastCommand.ScheduleBatch(cmds, results, 1, dependancy);

        return new RaycastJobHandler(numCommands, binSamples, results, cmds, h);

        }

        public void writeToFile(AllFloatData currData,
            RayCastRecorder recorder, Transform robot,
            bool isLastSampleInFrame) {
            // TODO : Move writing out of this class
            // TODO : have whatever class handles writing remember vars that make sense to remember
            int lastGazeIndex = numSamples - 2;
            // I also do not know why 2 specifically
            // -Xavier, 130923

            for (int i = 0; i < numSamples; i++) {
                if (i == lastGazeIndex && currData is MessageEvent) {
                    recorder.FlagEvent(((MessageEvent)currData).message);
                }

                Fsample curEyeData = binSamples[i];
                RaycastHit curRaycastHit = results[i];
                if (curRaycastHit.collider != null) {
                    Transform hitObj = curRaycastHit.transform;
                    //TODO
                    recorder.WriteSample(
                            type: curEyeData.dataType,
                            time: curEyeData.time,
                            objName: hitObj.name,
                            centerOffset: ScreenSaver.ComputeLocalPostion(hitObj, curRaycastHit),
                            hitObjLocation: hitObj.position,
                            pointHitLocation: curRaycastHit.point,
                            rawGaze: curEyeData.rawRightGaze,
                            subjectLoc: robot.position,
                            subjectRotation: robot.rotation.eulerAngles.y,
                            isLastSampleInFrame: i == lastGazeIndex && isLastSampleInFrame);
                }
                else {
                    recorder.IgnoreSample(
                            type: curEyeData.dataType,
                            time: curEyeData.time,
                            rawGaze: curEyeData.rawRightGaze,
                            subjectLoc: robot.position,
                            subjectRotation: robot.rotation.eulerAngles.y,
                            isLastSampleInFrame: i == lastGazeIndex && isLastSampleInFrame
                        );
                }

            }

        }
        public void Dispose() {
            results.Dispose();
            cmds.Dispose();
        }
           
    }

}