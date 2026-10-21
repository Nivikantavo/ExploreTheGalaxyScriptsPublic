using UnityEngine;

public class GoalFocusTarget : MonoBehaviour
{
    [SerializeField] private string _targetId;

    public string TargetId => _targetId;
}
