using UnityEngine;
using UnityEngine.UI;

public class ShopTabBarView : MonoBehaviour
{
    [SerializeField] private Image[] _tabImages;
    [SerializeField] private Sprite _activeSprite;
    [SerializeField] private Sprite _inactiveSprite;

    public void SetSelected(int index)
    {
        if (_tabImages == null || _tabImages.Length == 0)
            return;

        for (int i = 0; i < _tabImages.Length; i++)
        {
            Image image = _tabImages[i];
            if (image == null)
                continue;

            Sprite sprite = i == index ? _activeSprite : _inactiveSprite;
            if (sprite != null)
                image.sprite = sprite;
        }
    }
}
