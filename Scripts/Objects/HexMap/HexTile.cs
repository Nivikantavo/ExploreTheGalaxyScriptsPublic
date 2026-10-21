using System;
using ECM2;
using PrimeTween;
using UnityEngine;

public class HexTile : MonoBehaviour
{
    public event Action OnUnlockAnimationEnded;
    public event Action<HexTile> OnUnlocked;

    [SerializeField] private int _q;
    [SerializeField] private int _r;
    [SerializeField] private float _height;
    [SerializeField] private bool _startsLocked;
    [SerializeField] private float _unlockDuration;

    public int Q => _q;
    public int R => _r;
    public float Height => _height;
    public bool StartsLocked => _startsLocked;
    public bool IsLocked => _isLocked;
    private bool _isLocked = true;
    public void Initialize(int q, int r, float height, bool startsLocked)
    {
        _q = q;
        _r = r;
        _height = height;
        _startsLocked = startsLocked;
    }

    public void SetStartsLocked(bool startsLocked)
    {
        _startsLocked = startsLocked;
    }

    private void Awake()
    {
        // `Initialize()` is called before `Awake()` for tiles created at runtime.
        // If the tile is not supposed to be locked from the start, consider it unlocked.
        _isLocked = _startsLocked;

        if (_startsLocked && _isLocked)
            gameObject.SetActive(false);
    }

    public void Unlock()
    {
        if (!_isLocked)
            return;

        _isLocked = false;
        // Keep spawners off while the hex scales in. They sit on this tile, so activating
        // the hex would otherwise wake them on the first frame of the unlock tween.
        GameObject[] deferredSpawnPoints = DeactivateChildSpawnPoints();
        Vector3 targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);

        // Disable solid colliders while the mesh scales up — IgnoreCollision + MTD fail on
        // non-convex MeshColliders and leave the player embedded in the new cliff face.
        Collider[] colliders = HexUnlockPlayerDepenetration.CollectSolidColliders(gameObject);
        bool[] enabledStates = new bool[colliders.Length];
        HexUnlockPlayerDepenetration.StoreEnabledStates(colliders, enabledStates);
        HexUnlockPlayerDepenetration.SetCollidersEnabled(colliders, false);

        CharacterMovement movement = HexUnlockPlayerDepenetration.FindPlayerMovement();
        Vector3 hexCenter = transform.position;

        OnUnlocked?.Invoke(this);

        Tween.Scale(transform, targetScale, _unlockDuration, Ease.InOutSine).OnComplete(() =>
        {
            HexUnlockPlayerDepenetration.RestoreEnabledStates(colliders, enabledStates);
            HexUnlockPlayerDepenetration.DepenetrateFromHex(movement, hexCenter, colliders);
            ActivateSpawnPoints(deferredSpawnPoints);
            OnUnlockAnimationEnded?.Invoke();
        });
    }

    public void UnlockFromSave()
    {
        if (!_isLocked)
            return;

        _isLocked = false;
        gameObject.SetActive(true);
        OnUnlocked?.Invoke(this);
        OnUnlockAnimationEnded?.Invoke();
    }

    private GameObject[] DeactivateChildSpawnPoints()
    {
        EnemySpawnPoint[] points = GetComponentsInChildren<EnemySpawnPoint>(true);
        int count = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (ShouldDeferSpawnPoint(points[i]))
                count++;
        }

        var deferred = new GameObject[count];
        int index = 0;
        for (int i = 0; i < points.Length; i++)
        {
            EnemySpawnPoint point = points[i];
            if (!ShouldDeferSpawnPoint(point))
                continue;

            deferred[index++] = point.gameObject;
            point.gameObject.SetActive(false);
        }

        return deferred;
    }

    private bool ShouldDeferSpawnPoint(EnemySpawnPoint point)
    {
        return point != null && point.gameObject != gameObject && point.gameObject.activeSelf;
    }

    private static void ActivateSpawnPoints(GameObject[] spawnPoints)
    {
        if (spawnPoints == null)
            return;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
                spawnPoints[i].SetActive(true);
        }
    }
}
