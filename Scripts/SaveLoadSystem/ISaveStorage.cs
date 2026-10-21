using System;

public interface ISaveStorage
{
    SaveStorageInitState InitState { get; }
    bool HasSave();
    void Save(string json);
    void SaveBothSlots(string json);
    string Load();
    string LoadPrevious();
    void Delete();
}

public interface IInitializableSaveLoader
{
    bool IsInitialized { get; }
    SaveStorageInitState InitState { get; }
    event Action LateDataArrived;
    void Initialize(Action onComplete);
    void ForceCompleteInitialization();
}
