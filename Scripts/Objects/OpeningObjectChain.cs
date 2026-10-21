using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OpeningObjectChain : MonoBehaviour
{
    public string ChainSaveId => _chainSaveId;
    public IReadOnlyList<OpeningObject> Steps => _steps;

    public bool AreAllStepsFilled()
    {
        if (_steps == null || _steps.Length == 0)
            return false;

        for (int i = 0; i < _steps.Length; i++)
        {
            OpeningObject step = _steps[i];
            if (step == null || (!step.IsCompleted && !step.IsFilled))
                return false;
        }

        return true;
    }

    [SerializeField] private string _chainSaveId;
    [SerializeField] private OpeningObject _firstStep;

    private OpeningObject[] _steps = System.Array.Empty<OpeningObject>();

    private void Awake()
    {
        CacheSteps();
    }

    public OpeningObjectChainSaveData CreateSaveData()
    {
        if (string.IsNullOrEmpty(_chainSaveId) || _steps.Length == 0)
            return null;

        int completedStepsCount = 0;
        while (completedStepsCount < _steps.Length && _steps[completedStepsCount].IsCompleted)
            completedStepsCount++;

        if (completedStepsCount >= _steps.Length)
        {
            return new OpeningObjectChainSaveData
            {
                ChainSaveId = _chainSaveId,
                CompletedStepsCount = _steps.Length
            };
        }

        OpeningObject currentStep = _steps[completedStepsCount];
        if (currentStep.IsFilled)
        {
            return new OpeningObjectChainSaveData
            {
                ChainSaveId = _chainSaveId,
                CompletedStepsCount = completedStepsCount + 1
            };
        }

        if (completedStepsCount == 0 && !currentStep.HasPaymentProgress)
            return null;

        return new OpeningObjectChainSaveData
        {
            ChainSaveId = _chainSaveId,
            CompletedStepsCount = completedStepsCount,
            RemainsToPay = SaveDataConversion.ToSaveArray(currentStep.RemainsToPay),
            AccumulatedTransferTime = currentStep.AccumulatedTransferTime
        };
    }

    public void ApplySave(OpeningObjectChainSaveData saveData)
    {
        if (saveData == null || string.IsNullOrEmpty(saveData.ChainSaveId) || _steps.Length == 0)
            return;

        int completedStepsCount = Mathf.Clamp(saveData.CompletedStepsCount, 0, _steps.Length);

        for (int i = 0; i < completedStepsCount; i++)
            _steps[i].ApplyCompletedFromSave();

        if (completedStepsCount >= _steps.Length)
            return;

        _steps[completedStepsCount].ApplyInProgressFromSave(
            saveData.RemainsToPay,
            saveData.AccumulatedTransferTime);

        for (int i = completedStepsCount + 1; i < _steps.Length; i++)
            _steps[i].ApplyLockedFromSave();
    }

    private void CacheSteps()
    {
        if (_firstStep == null)
            _firstStep = GetComponentInChildren<OpeningObject>(true);

        if (_firstStep == null)
        {
            _steps = System.Array.Empty<OpeningObject>();
            return;
        }

        var steps = new List<OpeningObject>();
        var visited = new HashSet<OpeningObject>();
        OpeningObject currentStep = _firstStep;

        while (currentStep != null && visited.Add(currentStep))
        {
            steps.Add(currentStep);
            currentStep = currentStep.GetNextStep();
        }

        _steps = steps.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(_chainSaveId))
            return;

        _chainSaveId = gameObject.name;
    }
#endif
}
