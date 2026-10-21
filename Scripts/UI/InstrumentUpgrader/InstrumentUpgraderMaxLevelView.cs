using UnityEngine;
using UnityEngine.UI;

public class InstrumentUpgraderMaxLevelView : MonoBehaviour
{
    [SerializeField] private Image _image;

    public void Initialize(InstrumentUpgraderData data)
    {
        _image.sprite = data.NextLevelIcon;
    }
}
