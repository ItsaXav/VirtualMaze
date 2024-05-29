using System.Xml;
using System.Transactions;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Eyelink.Structs;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using VirtualMaze.Assets.Scripts.Utils;
using UnityEditor;
using System.Runtime.CompilerServices;
using VirtualMaze.Assets.Scripts.Utils;

namespace VirtualMaze.Assets.Scripts.Raycasting {

    // TODO : Implement an async that manages its own memory and IO and async processes then writes 
    // Idea : Job that schedules jobs for raycasting in a fixation, then schedules a job to write to file
    // need to expose job method in a recorder class
    
    public class AreaRaycastManager
    {


        

        float angularRadius;
        float angularStepSize;
        float distToScreen;
        Rect screenDims;
        Rect pixelDims;
        public static float SCENE_MAX_DIST = 7.5f;
        // based of tan(30) * 25 units, rounded up to give a buffer.

        public AreaRaycastManager(
            float angularRadius, float angularStepSize, float distToScreen, 
                Rect screenDims, Rect pixelDims) {


            this.angularRadius = angularRadius;
            this.angularStepSize = angularStepSize;
            this.distToScreen = distToScreen;
            this.screenDims = screenDims;
            this.pixelDims = pixelDims;

        }
        public static readonly OffsetData NULL_OFFSET = new OffsetData(Vector2.negativeInfinity, Vector2.negativeInfinity);

        public struct OffsetData {
            public Vector2 AngularOffset;
            public Vector2 PixelOffset;
            public OffsetData(Vector2 angularOffset, Vector2 pixelOffset) {
                this.AngularOffset = angularOffset;
                this.PixelOffset = pixelOffset;
            }
        }

        public Tuple<RaycastHit[],Fsample[],OffsetData[]> ScheduleAreaCasting(Fsample sampleToCast, Camera viewport, JobHandle dependency = default) {
            if (sampleToCast.RightGaze.isNaN()) {
                return Tuple.Create(new RaycastHit[] {}, new Fsample[] {}, new OffsetData[] {});
                // return empty if the sample gaze is nan (has any nan component)
            }
            Debug.LogError($"Starting areacast with camera @ {viewport.transform.position}");
                
            Tuple<List<Fsample>,List<OffsetData>> angleResults = generateFromAngle(sampleToCast);
            List<Fsample> sampleList = angleResults.Item1;
            List<OffsetData> offsetList = angleResults.Item2;
            List<Ray> rays = ConvertToRays(sampleList, viewport);

            // NativeArray<RaycastCommand> raycastCommands = 
            //     new NativeArray<RaycastCommand>(rays.Count, Allocator.TempJob);
            // NativeArray<RaycastHit> resultsAsNative = new NativeArray<RaycastHit>(rays.Count, Allocator.TempJob);
            RaycastHit[] results = new RaycastHit[rays.Count];
            Debug.LogError($"First ray : {rays[0].origin}, {rays[0].direction}");
            
            for (int i = 0; i < rays.Count; i++) {
                Ray ray = rays[i];
                
                if (RayConstants.IsAbsoluteEqual(ray, RayConstants.NullRay)) {
                    continue;
                     // will leave the raycast command as null, no raycasting will be performed here.
                }
                Physics.Raycast(ray : ray, hitInfo : out results[i], maxDistance : float.MaxValue, layerMask : BinWallManager.ignoreBinningLayer);            }
            
            // JobHandle raycastHandle = RaycastCommand.ScheduleBatch(
            //     raycastCommands, resultsAsNative, 1, dependency);
            
            // raycastHandle.Complete();
            UnityEngine.Debug.Log($"Completed areacast for {sampleToCast}");
            UnityEngine.Debug.Log($"Camera was at {viewport.transform.position}");
            Fsample[] samples = sampleList.ToArray();
            OffsetData[] offsets = offsetList.ToArray();
            return Tuple.Create(results,samples,offsets);
        }

        public Tuple<RaycastHit[],Fsample[],OffsetData[]> ScheduleDummyCasting(Fsample sampleToCast, Camera viewport, JobHandle dependency = default) {
            if (sampleToCast.RightGaze.isNaN()) {
                return Tuple.Create(new RaycastHit[] {}, new Fsample[] {}, new OffsetData[] {});
                // return empty if the sample gaze is nan (has any nan component)
            }
            List<Fsample> sampleList = new List<Fsample>();
            sampleList.Add(sampleToCast); // Add fsample to sampleList
            List<OffsetData> offsetList = new List<OffsetData>();
            offsetList.Add(new OffsetData(new Vector2(0,0), new Vector2(0,0)));
            List<Ray> rays = ConvertToRays(sampleList, viewport);
            /* 107 x 72
            827 x 257
            gap of 12 
            30px
            ~248 x 170*/
            // NativeArray<RaycastCommand> raycastCommands = 
            //     new NativeArray<RaycastCommand>(rays.Count, Allocator.TempJob);
            // NativeArray<RaycastHit> resultsAsNative = new NativeArray<RaycastHit>(rays.Count, Allocator.TempJob);
            RaycastHit[] results = new RaycastHit[rays.Count];
            Debug.Log($"First ray : {rays[0].origin}, {rays[0].direction}");
            for (int i = 0; i < rays.Count; i++) {
                Ray ray = rays[i];
                
                if (RayConstants.IsAbsoluteEqual(ray, RayConstants.NullRay)) {
                    continue;
                     // will leave the raycast command as null, no raycasting will be performed here.
                }
                Physics.Raycast(ray : ray, hitInfo : out results[i], maxDistance : float.MaxValue, layerMask : BinWallManager.ignoreBinningLayer);
                
            }
            
            // JobHandle raycastHandle = RaycastCommand.ScheduleBatch(
            //     raycastCommands, resultsAsNative, 1, dependency);
            
            // raycastHandle.Complete();
            //
            UnityEngine.Debug.Log($"Completed (dummy) areacast for {sampleToCast}, did not do in a radius due to center being hint/cue image.");

            Fsample[] samples = sampleList.ToArray();
            OffsetData[] offsets = offsetList.ToArray();
            return Tuple.Create(results,samples,offsets);
        }

        public void ScheduleHitWriteAndDispose(
            uint time,
            RaycastHit[] results,
            Fsample[] raycastSamples,
            OffsetData[] offsets,
            Tuple<RaycastHit[], Fsample[], OffsetData[]> resultsTask,
            Vector3 subjectLoc,
            Vector2 rawGaze,
            RayCastWriteManager writeManager) {

            // Ensure resultsTask is completed synchronously
            void WriteTask(RaycastHit hit, Fsample fsample, OffsetData offset) {
                string objName = RelativeHitLocFinder.getChainedName(hit.transform.gameObject);
                Vector3 hitLoc = hit.point;
                RayCastWriteData toWrite = new RayCastWriteDataBuilder()
                    .WithTime(time)
                    .WithSubjectLoc(subjectLoc)
                    .WithRawGaze(fsample.RightGaze)
                    .WithHitObjLocation(hitLoc)
                    .WithObjName(objName)
                    .WithType(fsample.dataType)
                    .WithAngularOffset(offset.AngularOffset)
                    .WithPixelOffset(offset.PixelOffset)
                    .Build();

                writeManager.Write(toWrite);
            }

            void NullWriteTask() {
                string objName = "NaN(Null RaycastHit)";
                Vector3 hitLoc = new Vector3(0, 0, 0);
                RayCastWriteData toWrite = new RayCastWriteDataBuilder()
                    .WithTime(time)
                    .WithSubjectLoc(subjectLoc)
                    .WithRawGaze(rawGaze)
                    .WithHitObjLocation(hitLoc)
                    .WithObjName(objName)
                    .WithType(DataTypes.SAMPLENODATA)
                    .WithAngularOffset(NULL_OFFSET.AngularOffset)
                    .WithPixelOffset(NULL_OFFSET.PixelOffset)
                    .Build();

                writeManager.Write(toWrite);

            }

            if (results.Length == 0) {
                UnityEngine.Debug.Log($"Wrote NaN result");
                string objName = "NaN(NaN Gaze)";
                Vector3 hitLoc = new Vector3(0, 0, 0);
                RayCastWriteData toWrite = new RayCastWriteDataBuilder()
                    .WithTime(time)
                    .WithSubjectLoc(subjectLoc)
                    .WithRawGaze(rawGaze)
                    .WithHitObjLocation(hitLoc)
                    .WithObjName(objName)
                    .WithType(DataTypes.SAMPLENODATA)
                    .WithAngularOffset(NULL_OFFSET.AngularOffset)
                    .WithPixelOffset(NULL_OFFSET.PixelOffset)
                    .Build();

                writeManager.Write(toWrite);

            }

            UnityEngine.Debug.Log($"Trying to write {results.Length} many non-NaN results");
            for (int i = 0; i < results.Length; i++) {
                RaycastHit hit = results[i];
                Fsample fsample = raycastSamples[i];
                OffsetData offset = offsets[i];
                if (hit.transform == null) {
                    UnityEngine.Debug.Log($"null raycasthit");
                    NullWriteTask();
                    continue;
                }
                WriteTask(hit, fsample, offset);
            }

            UnityEngine.Debug.Log($"Wrote {results.Length} many non-NaN results");

        }


        private List<Ray> ConvertToRays(List<Fsample> toConvert, Camera viewport) {
            // Pre-allocate the memory because we know the exact length of the output list
            List<Ray> outList = new List<Ray>(toConvert.Count);
            foreach (Fsample fsample in toConvert) {
                if (ScreenSaver.IsInScreenBounds(fsample.RightGaze) && fsample.dataType != DataTypes.SAMPLEINVALID) {
                    Vector2 viewportGaze = RangeCorrector.HD_TO_VIEWPORT.
                        correctVector(fsample.RightGaze);
                    outList.Add(viewport.ViewportPointToRay(viewportGaze));
                } else {
                    outList.Add(RayConstants.NullRay);
                }
            }
            return outList;
        }
        private Tuple<List<Fsample>,List<OffsetData>> generateFromAngle(Fsample sample) {
                float stepSize = this.angularStepSize;
                List<Fsample> outList = new List<Fsample>();
                List<OffsetData> offsetList = new List<OffsetData>();
                RangeCorrector pixelToCm = new RangeCorrector(pixelDims, screenDims);
                RangeCorrector CmToPixel = new RangeCorrector(screenDims, pixelDims);
                Vector2 CmOffset = pixelToCm.correctVector(sample.RightGaze);
                Vector2 centerLoc = new Vector2 (screenDims.x / 2, screenDims.y / 2);
                float xRadianLoc = Mathf.Atan2(CmOffset.x,distToScreen) - Mathf.Atan2(centerLoc.x, distToScreen);
                float yRadianLoc = Mathf.Atan2(CmOffset.y,distToScreen) - Mathf.Atan2(centerLoc.y, distToScreen);


                if (angularRadius <= 0 || angularRadius <= stepSize) {
                    outList.Add(sample);
                    offsetList.Add(new OffsetData(new Vector2(0, 0), new Vector2(0, 0)));
                    return Tuple.Create(outList,offsetList);
                }
                // create all possible offsets here
                List<Vector2> angleOffsets = new List<Vector2>();
                for (float i = 0 ; i <= angularRadius; i += stepSize) {
                    for (float j = 0; j <= angularRadius; j += stepSize) {
                        if (i * i + j * j > angularRadius * angularRadius) {
                            continue;
                        }

                        
                        angleOffsets.Add(new Vector2(i, j));
                        if (i != 0) {
                            angleOffsets.Add(new Vector2(-i, j));
                        }
                        if (j != 0) {
                            angleOffsets.Add(new Vector2(i, -j));
                        }
                        if (i != 0 && j != 0) {
                            angleOffsets.Add(new Vector2(-i, -j));
                        }
                    }
                }

                // iterate through all possible angular offsets ...
                
                foreach (Vector2 angularOffset in angleOffsets) {
                        
                       

                        // Generate new Fsample based on angular offset
                        float xRadianNew = xRadianLoc + Mathf.Deg2Rad * angularOffset.x;
                        float yRadianNew = yRadianLoc + Mathf.Deg2Rad * angularOffset.y;
                        
                        float xCmNew = Mathf.Tan(xRadianNew) * distToScreen;
                        float yCmNew = Mathf.Tan(yRadianNew) * distToScreen;

                        Vector2 vectorCmNew = new Vector2(xCmNew, yCmNew);
                        // the new location of the gaze in cm coordinates


                        // do bounds-checking & flagging here
                        DataTypes datatype = default;
                        if (!screenDims.Contains(vectorCmNew)) {
                            datatype = DataTypes.SAMPLEINVALID;
                            // flag if it is somehow invalid.
                        } else {
                            datatype = sample.dataType;
                        }

                        // back-compute the correct pixel value here.
                        Vector2 vectorPixelNew  = CmToPixel.correctVector(vectorCmNew);
                        //Vector2 vectorPixelRounded = roundToQuarterPixel(vectorPixelNew);
                        vectorPixelNew.y = pixelDims.height - vectorPixelNew.y;
                        Vector2 pixelOffset = vectorPixelNew - sample.rawRightGaze;
                        // use rawRightGaze here, as the constructor for Fsample assumes
                        // it is raw, in the eyelink convention for x and y.
                        // Conversion is handled elsewhere, don't need to worry.
                        // Similar for why we need to take height - y in the computation above.
                        Fsample temp = new Fsample(sample.time,
                            vectorPixelNew.x,
                            vectorPixelNew.y, 
                            datatype);
                        // take height - y, to obey convention of eyelink
                        // Unity takes 
                        outList.Add(temp);
                        offsetList.Add(new OffsetData(angularOffset,pixelOffset));
                        
                    
                }
                
                return Tuple.Create(outList,offsetList);
        }
        private Vector2 roundToQuarterPixel(Vector2 gazeXY) {
            float rawX = gazeXY.x;
            float rawY = gazeXY.y;

            float roundedX = Mathf.Round(rawX * 4) / 4;
            float roundedY = Mathf.Round(rawY * 4) / 4; 
            // multiplying by four, rounding to whole number, and dividing by 4 rounds to .25

            return new Vector2(roundedX, roundedY);
        }


    }

}
