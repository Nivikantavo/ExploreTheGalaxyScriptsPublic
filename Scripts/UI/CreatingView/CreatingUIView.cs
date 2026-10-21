using PrimeTween;
using UnityEngine;

public class CreatingUIView : WorldSpaceUI
{
    private const float ScaleDuration = 0.5f;
    private const float ShownScaleEpsilonSqr = 0.0001f;

    private int _requestId;

    public override void Show()
    {
        _requestId++;
        Tween.StopAll(transform);

        bool wasInactive = !gameObject.activeSelf;
        gameObject.SetActive(true);

        if (wasInactive)
            transform.localScale = Vector3.zero;

        if (IsShownScale(transform.localScale))
            return;

        Tween.Scale(transform, Vector3.one, ScaleDuration, Ease.InOutBack);
    }

    public override void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        int requestId = ++_requestId;
        Tween.StopAll(transform);

        if (transform.localScale.sqrMagnitude <= ShownScaleEpsilonSqr)
        {
            gameObject.SetActive(false);
            return;
        }

        Tween.Scale(transform, Vector3.zero, ScaleDuration, Ease.InOutBack)
            .OnComplete(() =>
            {
                if (requestId != _requestId)
                    return;

                gameObject.SetActive(false);
            });
    }

    private static bool IsShownScale(Vector3 scale)
    {
        return (scale - Vector3.one).sqrMagnitude <= ShownScaleEpsilonSqr;
    }
}
