using UnityEditor;
using UnityEngine;

public class HexMapEditorPreview : System.IDisposable
{
    private GameObject _instance;
    private GameObject _prefab;
    private Material _previewMaterial;

    public GameObject Prefab => _prefab;

    public void SetPrefab(GameObject prefab)
    {
        if (_prefab == prefab)
            return;

        DisposeInstance();
        _prefab = prefab;
    }

    public void UpdateTransform(Vector3 worldPosition, Quaternion worldRotation, bool visible)
    {
        if (_prefab == null || !visible)
        {
            Hide();
            return;
        }

        EnsureInstance();
        _instance.SetActive(true);
        _instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    public void Hide()
    {
        if (_instance != null)
            _instance.SetActive(false);
    }

    public void Dispose()
    {
        DisposeInstance();
        DisposeMaterial();
    }

    private void EnsureInstance()
    {
        if (_instance != null || _prefab == null)
            return;

        _instance = PrefabUtility.InstantiatePrefab(_prefab) as GameObject;
        _instance.name = $"{_prefab.name} (Preview)";
        _instance.hideFlags = HideFlags.HideAndDontSave;

        DisableBehaviours(_instance);
        DisableColliders(_instance);
        ApplyPreviewMaterial(_instance);
    }

    private void DisposeInstance()
    {
        if (_instance != null)
        {
            Object.DestroyImmediate(_instance);
            _instance = null;
        }

        _prefab = null;
    }

    private void DisposeMaterial()
    {
        if (_previewMaterial != null)
        {
            Object.DestroyImmediate(_previewMaterial);
            _previewMaterial = null;
        }
    }

    private void ApplyPreviewMaterial(GameObject root)
    {
        EnsurePreviewMaterial();

        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = _previewMaterial;
            renderer.sharedMaterials = materials;
        }
    }

    private void EnsurePreviewMaterial()
    {
        if (_previewMaterial != null)
            return;

        var shader = Shader.Find("Standard");
        _previewMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _previewMaterial.SetFloat("_Mode", 3f);
        _previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _previewMaterial.SetInt("_ZWrite", 0);
        _previewMaterial.DisableKeyword("_ALPHATEST_ON");
        _previewMaterial.EnableKeyword("_ALPHABLEND_ON");
        _previewMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _previewMaterial.renderQueue = 3000;
        _previewMaterial.color = new Color(0.4f, 0.9f, 1f, 0.45f);
    }

    private static void DisableColliders(GameObject root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>())
            collider.enabled = false;
    }

    private static void DisableBehaviours(GameObject root)
    {
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>())
            behaviour.enabled = false;
    }
}
