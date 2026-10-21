using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OccluderFadeTarget : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int OcclusionFadeId = Shader.PropertyToID("_OcclusionFade");

    [SerializeField] private float _transparentAlpha = 0.35f;
    [SerializeField] private float _fadeSpeed = 10f;
    [SerializeField] private bool _autoCreateCollider = true;
    [SerializeField, Range(0.2f, 1f)] private float _colliderBoundsScale = 0.6f;
    [SerializeField] private Collider _occluderCollider;

    private readonly List<Renderer> _renderers = new();
    private readonly List<Material[]> _originalMaterials = new();
    private readonly List<Material[]> _fadeMaterialSlots = new();
    private readonly List<MaterialPropertyBlock> _propertyBlocks = new();
    private readonly List<bool> _usingFadeMaterials = new();

    private float _currentFade = 1f;
    private float _targetFade = 1f;

    public float CurrentFade => _currentFade;
    public bool NeedsUpdate => !Mathf.Approximately(_currentFade, _targetFade) || _currentFade < 0.999f;

    private void Awake()
    {
        CacheRenderers();
        SetupCollider();
    }

    private void OnDisable()
    {
        CameraOccluderFade.Unregister(this);
        RestoreAllMaterials();
        _currentFade = 1f;
        _targetFade = 1f;
    }

    public void SetOccluding(bool occluding)
    {
        _targetFade = occluding ? _transparentAlpha : 1f;
    }

    public void TickFade(float deltaTime, Material fadeMaterial)
    {
        if (Mathf.Approximately(_currentFade, _targetFade))
            return;

        _currentFade = Mathf.MoveTowards(_currentFade, _targetFade, _fadeSpeed * deltaTime);
        ApplyFade(fadeMaterial);
    }

    private void CacheRenderers()
    {
        _renderers.Clear();
        _originalMaterials.Clear();
        _fadeMaterialSlots.Clear();
        _propertyBlocks.Clear();
        _usingFadeMaterials.Clear();

        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer is ParticleSystemRenderer or TrailRenderer or LineRenderer)
                continue;

            Material[] sharedMaterials = renderer.sharedMaterials;
            _renderers.Add(renderer);
            _originalMaterials.Add(sharedMaterials);
            _fadeMaterialSlots.Add(new Material[sharedMaterials.Length]);
            _propertyBlocks.Add(new MaterialPropertyBlock());
            _usingFadeMaterials.Add(false);
        }
    }

    private void SetupCollider()
    {
        bool createdByUs = false;
        if (_occluderCollider == null && _autoCreateCollider)
        {
            _occluderCollider = CreateBoundsCollider();
            createdByUs = _occluderCollider != null;
        }

        if (_occluderCollider == null)
        {
            Debug.LogWarning($"{nameof(OccluderFadeTarget)} on {name} has no occluder collider.", this);
            return;
        }

        // Auto-created fade colliders are dedicated triggers on OccluderFade layer.
        // Assigned Capsule/Mesh/etc. physics colliders are left unchanged (non-trigger, original layer).
        if (createdByUs)
        {
            _occluderCollider.gameObject.layer = OccluderFadeLayer.Layer;
            _occluderCollider.isTrigger = true;
        }
    }

    private Collider CreateBoundsCollider()
    {
        if (_renderers.Count == 0)
            return null;

        Bounds bounds = _renderers[0].bounds;
        for (int i = 1; i < _renderers.Count; i++)
            bounds.Encapsulate(_renderers[i].bounds);

        var colliderObject = new GameObject("OccluderFadeCollider");
        colliderObject.transform.SetParent(transform, false);
        colliderObject.layer = OccluderFadeLayer.Layer;

        var boxCollider = colliderObject.AddComponent<BoxCollider>();
        boxCollider.center = transform.InverseTransformPoint(bounds.center);
        boxCollider.size = bounds.size * _colliderBoundsScale;
        boxCollider.isTrigger = true;
        return boxCollider;
    }

    private void ApplyFade(Material fadeMaterial)
    {
        bool useFadeMaterial = _currentFade < 0.999f;

        for (int rendererIndex = 0; rendererIndex < _renderers.Count; rendererIndex++)
        {
            Renderer renderer = _renderers[rendererIndex];
            Material[] originals = _originalMaterials[rendererIndex];
            MaterialPropertyBlock propertyBlock = _propertyBlocks[rendererIndex];

            if (!useFadeMaterial)
            {
                if (_usingFadeMaterials[rendererIndex])
                {
                    renderer.sharedMaterials = originals;
                    renderer.SetPropertyBlock(null);
                    _usingFadeMaterials[rendererIndex] = false;
                }

                continue;
            }

            Material[] fadeMaterials = _fadeMaterialSlots[rendererIndex];
            for (int materialIndex = 0; materialIndex < fadeMaterials.Length; materialIndex++)
                fadeMaterials[materialIndex] = fadeMaterial;

            if (!_usingFadeMaterials[rendererIndex])
            {
                renderer.sharedMaterials = fadeMaterials;
                _usingFadeMaterials[rendererIndex] = true;
            }

            for (int materialIndex = 0; materialIndex < fadeMaterials.Length; materialIndex++)
            {
                Material source = originals[materialIndex];
                propertyBlock.Clear();
                propertyBlock.SetTexture(MainTexId, source.HasProperty(MainTexId) ? source.GetTexture(MainTexId) : Texture2D.whiteTexture);
                propertyBlock.SetColor(ColorId, source.HasProperty(ColorId) ? source.GetColor(ColorId) : Color.white);
                propertyBlock.SetFloat(OcclusionFadeId, _currentFade);
                renderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }

    private void RestoreAllMaterials()
    {
        for (int rendererIndex = 0; rendererIndex < _renderers.Count; rendererIndex++)
        {
            if (!_usingFadeMaterials[rendererIndex])
                continue;

            _renderers[rendererIndex].sharedMaterials = _originalMaterials[rendererIndex];
            _renderers[rendererIndex].SetPropertyBlock(null);
            _usingFadeMaterials[rendererIndex] = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider collider = _occluderCollider;
        if (collider == null)
            collider = GetComponentInChildren<Collider>();

        if (collider == null)
            return;

        // Opaque → cyan, fading/transparent → red
        float fade = Application.isPlaying ? _currentFade : 1f;
        Gizmos.color = Color.Lerp(new Color(1f, 0.25f, 0.25f, 0.9f), new Color(0.2f, 0.85f, 1f, 0.9f), fade);

        if (collider is BoxCollider box)
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = previous;
            return;
        }

        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }
}
