using System;

public abstract class SessionDataReader : IDisposable {
    SessionData CurrentData { get; }
    int CurrentIndex { get; }
    bool HasNext { get; }

    /// <summary>
    /// A value of 0 to 1 representing the read progress of the file.
    /// </summary>
    float ReadProgress { get; }

    bool Next();
    void MoveToNextTrigger();
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="trigger"></param>
    /// <returns></returns>
    public boolean moveToTrigger(SessionTrigger trigger) {
        while (this.Next()) {
            if (this.CurrentData.trigger = trigger) {
                return true;
            }
        }
        return false;
    }
}
