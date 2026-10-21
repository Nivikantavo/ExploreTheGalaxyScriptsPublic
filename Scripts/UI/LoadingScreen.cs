using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private bool _hideOnAwake = true;

    private void Awake()
    {
        if (_hideOnAwake)
            Hide();
    }

    public void Show()
    {
        if (_root != null)
            _root.SetActive(true);
        else
            gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);
        else
            gameObject.SetActive(false);
    }
}
