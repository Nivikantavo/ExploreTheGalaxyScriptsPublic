using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Tilts the flyover camera down after it has aligned to the rail.
/// </summary>
[DisallowMultipleComponent]
public class IslandFlyoverPitch : CinemachineExtension
{
    [SerializeField] private float _downwardPitch = 16f;

    public float DownwardPitch
    {
        get => _downwardPitch;
        set => _downwardPitch = value;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize)
            return;

        state.RawOrientation *= Quaternion.Euler(_downwardPitch, 0f, 0f);
    }
}
