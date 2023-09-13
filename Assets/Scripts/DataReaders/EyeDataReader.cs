using Eyelink.Structs;
using System;
using System.Collections.Generic;
namespace VirtualMaze.Assets.Scripts.DataReaders {
    public abstract class EyeDataReader {
        public abstract AllFloatData GetNextData();
        public abstract AllFloatData GetCurrentData();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="trigger"></param>
        ///   <returns></returns>
        public bool moveToNextTrigger(SessionTrigger trigger) {
            AllFloatData data = null;

            //move edfFile to point to first trial
            bool foundNextTrigger = false;
            while (!foundNextTrigger) {
                data = this.GetNextData();
                if (data == null) {
                    return false;
                }

                if (data.dataType == DataTypes.MESSAGEEVENT) {
                    MessageEvent ev = (MessageEvent)data;

                    foundNextTrigger = ev.trigger == trigger;
                }
                else if (data.dataType == DataTypes.NO_PENDING_ITEMS) {
                    foundNextTrigger = true;
                } 
            }
            return foundNextTrigger;
        }

        public List<AllFloatData> GetUntilTrigger(SessionTrigger targetTrigger) {
            List<AllFloatData> outList = new List<AllFloatData>();

            bool foundNextTrigger = false;
            while (!foundNextTrigger) {
                AllFloatData nextData = this.GetNextData();
                if (nextData == null) {
                    return outList;
                }
                // guard clause to ensure we don't add null to the list


                outList.Add(nextData);

                // I'm not too sure what these mean - Xavier,130923
                // Copied from ScreenSaver.cs pre-refactor
                // Probably has some relation to the eyelink data format
                // And the I/O for it implemented previously
                if (nextData.dataType == DataTypes.MESSAGEEVENT) {
                    MessageEvent ev = (MessageEvent)nextData;

                    foundNextTrigger = ev.trigger == targetTrigger;
                }
                else if (nextData.dataType == DataTypes.NO_PENDING_ITEMS) {
                    foundNextTrigger = true;
                } 
            }
            return outList;
        }

        public abstract void Dispose();
    }
}