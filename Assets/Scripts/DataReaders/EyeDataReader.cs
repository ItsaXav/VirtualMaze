using Eyelink.Structs;
using System;

public abstract class EyeDataReader : IDisposable{
      AllFloatData GetNextData();
      AllFloatData GetCurrentData();

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
            data = eyeReader.GetNextData();
            if (data.IsNullOrEmpty) {
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
        return foundNextTrigger
      }
}
