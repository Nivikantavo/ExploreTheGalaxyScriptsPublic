using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Promo flyover. The path itself is the child Rail spline, edited in the Scene view.
/// Disable or delete the Demo object to give control back to the gameplay camera.
/// </summary>
[DisallowMultipleComponent]
public class IslandFlyoverDemo : MonoBehaviour
{
    private const int FlyoverPriority = 100;

    [Header("Пролёт")]
    [Tooltip("Скорость камеры вдоль рельсы, в метрах в секунду.")]
    [Min(0f)]
    [SerializeField] private float _speed = 12f;

    [Tooltip("На сколько градусов камера смотрит вниз относительно рельсы.")]
    [Range(-15f, 50f)]
    [SerializeField] private float _downwardPitch = 16f;

    [Tooltip("После последнего острова начать пролёт сначала.")]
    [SerializeField] private bool _loop;

    [Tooltip("На время пролёта выключить экранный интерфейс. Мировые подписи остаются.")]
    [SerializeField] private bool _hideScreenUi = true;

    [Tooltip("Положение на рельсе в редакторе, от 0 в начале до 1 в конце. В Play не используется.")]
    [Range(0f, 1f)]
    [SerializeField] private float _preview;

    [HideInInspector] [SerializeField] private CinemachineCamera _flyoverCamera;
    [HideInInspector] [SerializeField] private CinemachineSplineDolly _dolly;
    [HideInInspector] [SerializeField] private IslandFlyoverPitch _pitch;

    private readonly List<CinemachineCamera> _suppressedCameras = new();
    private readonly List<Canvas> _hiddenCanvases = new();
    private CameraOccluderFade _occluderFade;
    private bool _occluderFadeWasEnabled;

    public float Speed => _speed;
    public float DownwardPitch => _downwardPitch;
    public bool Loop => _loop;
    public bool HideScreenUi => _hideScreenUi;
    public CinemachineSplineDolly Dolly => _dolly;

    public float PathLength
    {
        get
        {
            if (_dolly == null || _dolly.Spline == null)
                return 0f;

            return _dolly.Spline.CalculateLength();
        }
    }

    public void Bind(CinemachineCamera flyoverCamera, CinemachineSplineDolly dolly, IslandFlyoverPitch pitch)
    {
        _flyoverCamera = flyoverCamera;
        _dolly = dolly;
        _pitch = pitch;
        ApplyMotionSettings();
        ApplyPreviewPosition();
    }

    public void ApplyAuthoredSettings(float speed, float downwardPitch, bool loop, bool hideScreenUi)
    {
        _speed = Mathf.Max(0f, speed);
        _downwardPitch = downwardPitch;
        _loop = loop;
        _hideScreenUi = hideScreenUi;
        ApplyMotionSettings();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (_dolly != null)
            _dolly.CameraPosition = 0f;

        ApplyMotionSettings();
        SuppressGameplayCameras();
        SuppressOccluderFade();
        HideCanvases();
    }

    private void OnDisable()
    {
        RestoreGameplayCameras();
        RestoreOccluderFade();
        RestoreCanvases();
    }

    private void Update()
    {
        if (!Application.isPlaying || !_loop || _dolly == null)
            return;

        float length = PathLength;
        if (length > 0.01f && _dolly.CameraPosition >= length)
            _dolly.CameraPosition = 0f;
    }

    private void OnValidate()
    {
        _speed = Mathf.Max(0f, _speed);
        ApplyMotionSettings();

        if (!Application.isPlaying)
            ApplyPreviewPosition();
    }

    public void ApplyPreviewPosition()
    {
        if (_dolly == null || Application.isPlaying)
            return;

        float length = PathLength;
        _dolly.PositionUnits = PathIndexUnit.Distance;
        _dolly.CameraPosition = _preview * length;
    }

    private void ApplyMotionSettings()
    {
        if (_pitch != null)
            _pitch.DownwardPitch = _downwardPitch;

        if (_flyoverCamera != null)
        {
            PrioritySettings priority = _flyoverCamera.Priority;
            priority.Enabled = true;
            priority.Value = FlyoverPriority;
            _flyoverCamera.Priority = priority;
        }

        if (_dolly == null)
            return;

        _dolly.PositionUnits = PathIndexUnit.Distance;
        _dolly.CameraRotation = CinemachineSplineDolly.RotationMode.SplineNoRoll;

        SplineAutoDolly autoDolly = _dolly.AutomaticDolly;
        if (autoDolly.Method is not SplineAutoDolly.FixedSpeed fixedSpeed)
        {
            fixedSpeed = new SplineAutoDolly.FixedSpeed();
            autoDolly.Method = fixedSpeed;
        }

        fixedSpeed.Speed = _speed;
        autoDolly.Enabled = true;
        _dolly.AutomaticDolly = autoDolly;
    }

    private void SuppressGameplayCameras()
    {
        _suppressedCameras.Clear();
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null || camera == _flyoverCamera || !camera.enabled)
                continue;

            if (camera.transform.IsChildOf(transform))
                continue;

            camera.enabled = false;
            _suppressedCameras.Add(camera);
        }
    }

    private void RestoreGameplayCameras()
    {
        foreach (CinemachineCamera camera in _suppressedCameras)
        {
            if (camera != null)
                camera.enabled = true;
        }

        _suppressedCameras.Clear();
    }

    private void SuppressOccluderFade()
    {
        _occluderFade = FindAnyObjectByType<CameraOccluderFade>(FindObjectsInactive.Exclude);
        if (_occluderFade == null)
            return;

        _occluderFadeWasEnabled = _occluderFade.enabled;
        _occluderFade.enabled = false;
    }

    private void RestoreOccluderFade()
    {
        if (_occluderFade == null)
            return;

        _occluderFade.enabled = _occluderFadeWasEnabled;
        _occluderFade = null;
    }

    private void HideCanvases()
    {
        _hiddenCanvases.Clear();
        if (!_hideScreenUi)
            return;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.enabled || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            if (canvas.transform.IsChildOf(transform))
                continue;

            canvas.enabled = false;
            _hiddenCanvases.Add(canvas);
        }
    }

    private void RestoreCanvases()
    {
        foreach (Canvas canvas in _hiddenCanvases)
        {
            if (canvas != null)
                canvas.enabled = true;
        }

        _hiddenCanvases.Clear();
    }
}
