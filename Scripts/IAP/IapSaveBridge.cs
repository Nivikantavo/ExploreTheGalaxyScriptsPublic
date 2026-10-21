/// <summary>
/// Decouples SaveLoadSystem from optional IapService binding.
/// </summary>
public class IapSaveBridge
{
    private IapService _iapService;

    public void Bind(IapService iapService)
    {
        _iapService = iapService;
    }

    public void WriteToSaveData(GameSaveData saveData)
    {
        _iapService?.WriteToSaveData(saveData);
    }
}
