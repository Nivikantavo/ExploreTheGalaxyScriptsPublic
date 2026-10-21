using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public readonly struct HexPlacementInfo
{
    public bool IsValid { get; }
    public Vector2Int Hex { get; }
    public Vector3 WorldPosition { get; }
    public Vector3 LocalPosition { get; }
    public float SurfaceY { get; }
    public HexTile HexTile { get; }
    public Collider SurfaceCollider { get; }

    public HexPlacementInfo(
        bool isValid,
        Vector2Int hex,
        Vector3 worldPosition,
        Vector3 localPosition,
        float surfaceY,
        HexTile hexTile,
        Collider surfaceCollider)
    {
        IsValid = isValid;
        Hex = hex;
        WorldPosition = worldPosition;
        LocalPosition = localPosition;
        SurfaceY = surfaceY;
        HexTile = hexTile;
        SurfaceCollider = surfaceCollider;
    }

    public static HexPlacementInfo Invalid => new(false, default, default, default, 0f, null, null);
}

public static class HexMapEditorPlacementUtility
{
    private static readonly Dictionary<int, float> PrefabBottomOffsetCache = new();

    public static bool TryRaycastGroundHex(
        Ray ray,
        HexMapRoot mapRoot,
        float hexSize,
        out HexPlacementInfo placement)
    {
        placement = HexPlacementInfo.Invalid;

        if (mapRoot == null)
            return false;

        var hits = Physics.RaycastAll(ray, 500f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (!IsUnderGroundContainer(hit.collider.transform, mapRoot.Ground))
                continue;

            var hexTile = hit.collider.GetComponentInParent<HexTile>();
            if (hexTile == null)
                continue;

            var localHit = mapRoot.transform.InverseTransformPoint(hit.point);
            placement = new HexPlacementInfo(
                true,
                new Vector2Int(hexTile.Q, hexTile.R),
                hit.point,
                localHit,
                hit.point.y,
                hexTile,
                hit.collider);

            return true;
        }

        return TryRaycastInactiveGroundHex(ray, mapRoot, hexSize, out placement);
    }

    private static bool TryRaycastInactiveGroundHex(
        Ray ray,
        HexMapRoot mapRoot,
        float hexSize,
        out HexPlacementInfo placement)
    {
        placement = HexPlacementInfo.Invalid;
        float closestDistance = float.MaxValue;

        foreach (var hexTile in EnumerateGroundHexTiles(mapRoot))
        {
            var renderers = hexTile.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!renderer.bounds.IntersectRay(ray, out float distance))
                    continue;

                if (distance >= closestDistance)
                    continue;

                var hitPoint = ray.GetPoint(distance);
                var localHit = mapRoot.transform.InverseTransformPoint(hitPoint);
                var hex = new Vector2Int(hexTile.Q, hexTile.R);

                if (!IsPointInsideHex(localHit, hexTile, hexSize, mapRoot.transform))
                    continue;

                closestDistance = distance;
                placement = new HexPlacementInfo(
                    true,
                    hex,
                    hitPoint,
                    localHit,
                    hitPoint.y,
                    hexTile,
                    null);
            }
        }

        return placement.IsValid;
    }

    public static bool TryGetGroundPlanePlacement(
        Ray ray,
        HexMapRoot mapRoot,
        float hexSize,
        float groundHeight,
        out HexPlacementInfo placement)
    {
        placement = HexPlacementInfo.Invalid;

        var planeOrigin = mapRoot.transform.TransformPoint(new Vector3(0f, groundHeight, 0f));
        var plane = new Plane(Vector3.up, planeOrigin);

        if (!plane.Raycast(ray, out float distance))
            return false;

        var worldPos = ray.GetPoint(distance);
        var localPos = mapRoot.transform.InverseTransformPoint(worldPos);
        var hex = HexCoordinates.WorldToAxial(localPos, hexSize);

        placement = new HexPlacementInfo(
            true,
            hex,
            worldPos,
            localPos,
            worldPos.y,
            null,
            null);

        return true;
    }

    public static bool IsPointInsideHex(Vector3 localPoint, Vector2Int hex, float hexSize)
    {
        var hexCenter = HexCoordinates.AxialToWorld(hex.x, hex.y, hexSize);
        return IsPointInsideHexAtCenter(localPoint, hexCenter, hexSize);
    }

    public static bool IsPointInsideHex(Vector3 localPoint, HexTile hexTile, float hexSize, Transform mapRootTransform)
    {
        if (hexTile == null || mapRootTransform == null)
            return false;

        var hexCenter = mapRootTransform.InverseTransformPoint(hexTile.transform.position);
        hexCenter.y = localPoint.y;
        return IsPointInsideHexAtCenter(localPoint, hexCenter, hexSize);
    }

    private static bool IsPointInsideHexAtCenter(Vector3 localPoint, Vector3 hexCenterLocal, float hexSize)
    {
        var relative = new Vector3(localPoint.x - hexCenterLocal.x, 0f, localPoint.z - hexCenterLocal.z);
        var sampledHex = HexCoordinates.WorldToAxial(relative, hexSize);
        return sampledHex == Vector2Int.zero;
    }

    public static IEnumerable<HexTile> EnumerateGroundHexTiles(HexMapRoot mapRoot)
    {
        if (mapRoot == null || mapRoot.Ground == null)
            yield break;

        foreach (var hexTile in mapRoot.Ground.GetComponentsInChildren<HexTile>(true))
            yield return hexTile;
    }

    public static Vector3 GetHexCenterLocal(HexTile hexTile, Transform mapRootTransform)
    {
        if (hexTile == null || mapRootTransform == null)
            return Vector3.zero;

        return mapRootTransform.InverseTransformPoint(hexTile.transform.position);
    }

    public const float EnvironmentPlacementLift = 0.01f;

    public static Vector3 GetPlacementPosition(
        GameObject prefab,
        Vector3 surfaceWorldPoint,
        float rotationY,
        Transform parent,
        float surfaceLift = 0f)
    {
        float bottomOffset = GetPrefabBottomOffset(prefab, rotationY);
        var localSurface = parent.InverseTransformPoint(surfaceWorldPoint);
        var worldOffset = Vector3.up * (-bottomOffset + surfaceLift);
        var localPosition = localSurface + parent.InverseTransformVector(worldOffset);
        return parent.TransformPoint(localPosition);
    }

    public static Vector3 GetCompensatedLocalScale(Vector3 originalLocalScale, Transform parent, float extraFactor = 1f)
    {
        if (parent == null)
            return originalLocalScale * extraFactor;

        var parentScale = parent.localScale;
        return new Vector3(
            DivideByParentAxis(originalLocalScale.x * extraFactor, parentScale.x),
            DivideByParentAxis(originalLocalScale.y * extraFactor, parentScale.y),
            DivideByParentAxis(originalLocalScale.z * extraFactor, parentScale.z));
    }

    public static void ApplyCompensatedLocalScale(Transform instance, Transform parent, float extraFactor = 1f)
    {
        if (instance == null)
            return;

        instance.localScale = GetCompensatedLocalScale(instance.localScale, parent, extraFactor);
    }

    private static float DivideByParentAxis(float value, float parentAxis)
    {
        return Mathf.Approximately(parentAxis, 0f) ? value : value / parentAxis;
    }

    public static Quaternion GetPlacementRotation(float rotationY)
    {
        return Quaternion.Euler(0f, rotationY, 0f);
    }

    public static float GetPrefabBottomOffset(GameObject prefab, float rotationY = 0f)
    {
        if (prefab == null)
            return 0f;

        int id = prefab.GetInstanceID() ^ Mathf.RoundToInt(rotationY * 10f);
        if (PrefabBottomOffsetCache.TryGetValue(id, out float cached))
            return cached;

        var temp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, rotationY, 0f));

        float bottom = 0f;
        var renderers = temp.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            bottom = bounds.min.y;
        }

        Object.DestroyImmediate(temp);
        PrefabBottomOffsetCache[id] = bottom;
        return bottom;
    }

    public static GameObject PickPlacedObject(Vector2 mousePosition, HexMapRoot mapRoot, HexMapPaintMode? modeFilter)
    {
        var picked = HandleUtility.PickGameObject(mousePosition, false);
        if (picked == null || mapRoot == null)
            return null;

        if (!IsUnderMapRoot(picked.transform, mapRoot.transform))
            return null;

        var placed = picked.GetComponentInParent<HexMapPlacedObject>();
        if (placed == null)
            return null;

        if (modeFilter.HasValue && placed.PaintMode != modeFilter.Value)
            return null;

        return placed.gameObject;
    }

    public static float GetHexSurfaceY(HexMapRoot mapRoot, Vector2Int hex, float hexSize, float fallbackHeight)
    {
        var tile = FindGroundHex(mapRoot, hex);
        if (tile == null)
            return mapRoot.transform.TransformPoint(new Vector3(0f, fallbackHeight, 0f)).y;

        var renderers = tile.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return tile.transform.position.y;

        float maxY = float.MinValue;
        foreach (var renderer in renderers)
            maxY = Mathf.Max(maxY, renderer.bounds.max.y);

        return maxY;
    }

    public static GameObject FindGroundHex(HexMapRoot mapRoot, Vector2Int hex)
    {
        if (mapRoot == null)
            return null;

        foreach (var hexTile in EnumerateGroundHexTiles(mapRoot))
        {
            if (hexTile.Q == hex.x && hexTile.R == hex.y)
                return hexTile.gameObject;
        }

        return null;
    }

    public static Transform GetHexParent(HexMapRoot mapRoot, Vector2Int hex)
    {
        var groundHex = FindGroundHex(mapRoot, hex);
        return groundHex != null ? groundHex.transform : null;
    }

    public static List<Transform> CollectPlacedChildren(Transform hexTransform)
    {
        var children = new List<Transform>();
        if (hexTransform == null)
            return children;

        foreach (Transform child in hexTransform)
        {
            if (child.GetComponent<HexMapPlacedObject>() != null)
                children.Add(child);
        }

        return children;
    }

    public static void UpdateOpeningHexReferences(HexTile oldHex, HexTile newHex)
    {
        if (oldHex == null || newHex == null)
            return;

        foreach (var opener in Object.FindObjectsByType<OpeningHexagon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (opener.OpenedHexagon != oldHex)
                continue;

            var serializedObject = new SerializedObject(opener);
            serializedObject.FindProperty("_openedHexagon").objectReferenceValue = newHex;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool IsUnderGroundContainer(Transform target, Transform groundContainer)
    {
        if (target == null || groundContainer == null)
            return false;

        return target == groundContainer || target.IsChildOf(groundContainer);
    }

    private static bool IsUnderMapRoot(Transform target, Transform mapRoot)
    {
        return target == mapRoot || target.IsChildOf(mapRoot);
    }
}
