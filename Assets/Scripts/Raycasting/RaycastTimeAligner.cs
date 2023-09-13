// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;
// using Boo.Lang.Runtime;
// using Eyelink.Structs;
// using VirtualMaze.Assets.Scripts.DataReaders;

// namespace VirtualMaze.Assets.Scripts.Raycasting
// {
//     // because this is apparently 
//     // big enough of a problem to warrant a lot of code
//     public class RaycastTimeAligner
//     {
//         private decimal EnqueueData(Queue<SessionData> sessionFrames, 
//         SessionDataReader sessionReader,
//         Queue<AllFloatData> fixations,
//         EyeDataReader eyeReader) {
//         //Profiler.BeginSample("Enqueue");

        
//         decimal sessionEventPeriod = 
//         LoadToNextTriggerSession(sessionReader, sessionFrames, out SessionData sessionData);
//         uint edfEventPeriod = 
//         LoadToNextTriggerEdf(eyeReader, fixations, out MessageEvent edfdata, out SessionTrigger edfTrigger);

//         if (sessionData.trigger != edfTrigger) {
//             throw new RuntimeException("Unaligned session and eyedata! Are there missing triggers in eyelink or Unity data?");
//         }

//         decimal excessTime = (sessionEventPeriod - edfEventPeriod);

//         // status = No_Missing;
//         // reason = null;

//         /* if missing trigger detected and the time difference between the 2 files more than the threshold*/
//         // if (Math.Abs(excessTime) > Accepted_Time_Diff) {
//         //     status = Ignore_Data;
//         //     reason = $"Time difference too large.";
//         // }

//         //print($"ses: { sessionEventPeriod:F4}, edf: { edfEventPeriod:F4}, excess: {excessTime:F4} |{fixations.Peek().ToString()} {sessionFrames.Count}");
//         //Profiler.EndSample();
//         return excessTime;
//     }
//     }
// }