using UnityEngine;

[CreateAssetMenu(fileName = "SceneTravelDestination", menuName = "Scene Travel/Destination")]
public class SceneTravelDestination : ScriptableObject
{
    public string SceneName => _sceneName;
    public bool ReturnToHost => _returnToHost;

    [SerializeField] private string _sceneName;
    [SerializeField] private bool _returnToHost;
}
