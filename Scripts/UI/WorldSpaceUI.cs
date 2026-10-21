using UnityEngine;

public abstract class WorldSpaceUI : MonoBehaviour
{
    private static bool s_suppressAwakeHide;

    private Transform _mainCamera;
    public abstract void Show();
    public abstract void Hide();

    protected static void SetActiveWithoutAwakeHide(GameObject target)
    {
        s_suppressAwakeHide = true;
        try
        {
            target.SetActive(true);
        }
        finally
        {
            s_suppressAwakeHide = false;
        }
    }

    protected void HideOnAwake()
    {
        if (s_suppressAwakeHide)
            return;

        gameObject.SetActive(false);
    }

    protected virtual void Start()
    {
        TryResolveLookCamera();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            if (canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;

            JoystickWorldUIPassthrough.RegisterWorldSpaceCanvas(canvas);
        }
    }

    protected virtual void OnDestroy()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            JoystickWorldUIPassthrough.UnregisterWorldSpaceCanvas(canvas);
    }

    private void LateUpdate()
    {
        if (!TryResolveLookCamera())
            return;

        transform.LookAt(_mainCamera);
        transform.Rotate(0, 180, 0);
    }

    private bool TryResolveLookCamera()
    {
        if (_mainCamera != null)
            return true;

        Camera camera = Camera.main;
        if (camera == null)
            return false;

        _mainCamera = camera.transform;
        return _mainCamera != null;
    }
}
