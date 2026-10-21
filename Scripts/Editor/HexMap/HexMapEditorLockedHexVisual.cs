using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class HexMapEditorLockedHexVisual
{
    private static Material _lockedMaterial;
    private static readonly Dictionary<int, Material[]> OriginalMaterials = new();

    public static void RefreshAll(HexMapRoot mapRoot, bool showLockedVisual)
    {
        if (mapRoot == null)
            return;

        mapRoot.EnsureContainers();

        foreach (var hexTile in HexMapEditorPlacementUtility.EnumerateGroundHexTiles(mapRoot))
        {
            if (!hexTile.StartsLocked)
                continue;

            RefreshHex(hexTile.gameObject, showLockedVisual);
        }
    }

    public static void RefreshHex(GameObject hex, bool showLockedVisual)
    {
        if (hex == null)
            return;

        var hexTile = hex.GetComponent<HexTile>();
        if (hexTile == null)
            return;

        if (!hexTile.StartsLocked)
        {
            RestoreMaterials(hex);
            if (!EditorApplication.isPlaying)
                hex.SetActive(true);
            return;
        }

        if (!EditorApplication.isPlaying)
            hex.SetActive(true);

        if (showLockedVisual && !EditorApplication.isPlaying)
            ApplyLockedMaterial(hex);
        else
            RestoreMaterials(hex);
    }

    public static void ClearAll()
    {
        OriginalMaterials.Clear();
        DisposeMaterial();
    }

    private static void ApplyLockedMaterial(GameObject hex)
    {
        EnsureLockedMaterial();

        foreach (var renderer in hex.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsHexOwnRenderer(renderer, hex))
                continue;

            int id = renderer.GetInstanceID();
            if (!OriginalMaterials.ContainsKey(id))
                OriginalMaterials[id] = renderer.sharedMaterials;

            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = _lockedMaterial;
            renderer.sharedMaterials = materials;
        }
    }

    private static void RestoreMaterials(GameObject hex)
    {
        foreach (var renderer in hex.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsHexOwnRenderer(renderer, hex))
                continue;

            int id = renderer.GetInstanceID();
            if (!OriginalMaterials.TryGetValue(id, out var originals))
                continue;

            renderer.sharedMaterials = originals;
            OriginalMaterials.Remove(id);
        }
    }

    private static bool IsHexOwnRenderer(Renderer renderer, GameObject hex)
    {
        var placedObject = renderer.GetComponentInParent<HexMapPlacedObject>();
        return placedObject == null || placedObject.gameObject == hex;
    }

    private static void EnsureLockedMaterial()
    {
        if (_lockedMaterial != null)
            return;

        var shader = Shader.Find("Standard");
        _lockedMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _lockedMaterial.SetFloat("_Mode", 3f);
        _lockedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lockedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lockedMaterial.SetInt("_ZWrite", 0);
        _lockedMaterial.DisableKeyword("_ALPHATEST_ON");
        _lockedMaterial.EnableKeyword("_ALPHABLEND_ON");
        _lockedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _lockedMaterial.renderQueue = 3000;
        _lockedMaterial.color = new Color(1f, 0.45f, 0.15f, 0.55f);
    }

    private static void DisposeMaterial()
    {
        if (_lockedMaterial == null)
            return;

        Object.DestroyImmediate(_lockedMaterial);
        _lockedMaterial = null;
    }
}
