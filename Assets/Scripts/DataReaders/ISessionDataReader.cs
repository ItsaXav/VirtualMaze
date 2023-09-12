using System;

namespace VirtualMaze.Assets.Scripts.DataReaders{
public abstract class SessionDataReader : IDisposable {
    public SessionData CurrentData { get; protected set;}
    public int CurrentIndex { get; protected set; }
    public bool HasNext { get; protected set;}

    /// <summary>
    /// A value of 0 to 1 representing the read progress of the file.
    /// </summary>
    public float ReadProgress { get; protected set;}

    public abstract bool Next();

    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="trigger"></param>
    /// <returns></returns>
    public bool moveToTrigger(SessionTrigger trigger) {
        while (this.Next()) {
            if (this.CurrentData.trigger == trigger) {
                return true;
            }
        }
        return false;
    }

    public abstract void Dispose();
}
}