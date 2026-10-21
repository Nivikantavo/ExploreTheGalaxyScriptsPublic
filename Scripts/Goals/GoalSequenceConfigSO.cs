using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Goals/Goal Sequence Config", fileName = "GoalSequenceConfig")]
public class GoalSequenceConfigSO : ScriptableObject
{
    private const string World2AssetPath = "Assets/Configs/Goal/GoalSequenceConfigWorld2.asset";

    [SerializeField] private string _worldId = SaveWorldIds.World1;
    [SerializeField] private GoalSequenceConfigSO _nextSequence;
    [SerializeField] private List<GoalDefinitionSO> _goals = new();

    private bool _triedNextFallback;
    private GoalSequenceConfigSO _fallbackNext;

    public string WorldId => _worldId;
    public IReadOnlyList<GoalDefinitionSO> Goals => _goals;

    public GoalSequenceConfigSO NextSequence
    {
        get
        {
            if (_nextSequence != null)
                return _nextSequence;

            if (_triedNextFallback)
                return _fallbackNext;

            _triedNextFallback = true;
            _fallbackNext = LoadNextFallback();
            return _fallbackNext;
        }
    }

    private GoalSequenceConfigSO LoadNextFallback()
    {
        if (!string.Equals(GoalSequenceProgress.ResolveWorldId(this), SaveWorldIds.World1, StringComparison.Ordinal))
            return null;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GoalSequenceConfigSO>(World2AssetPath);
#else
        return null;
#endif
    }
}
