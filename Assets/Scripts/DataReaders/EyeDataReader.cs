using Eyelink.Structs;
using System;
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

        public abstract void Dispose();
    }
}