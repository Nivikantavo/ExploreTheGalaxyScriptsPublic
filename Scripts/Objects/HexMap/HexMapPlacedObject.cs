using UnityEngine;

public class HexMapPlacedObject : MonoBehaviour
{
    [SerializeField] private HexMapPaintMode _paintMode;
    [SerializeField] private int _q;
    [SerializeField] private int _r;

    public HexMapPaintMode PaintMode => _paintMode;
    public int Q => _q;
    public int R => _r;

    public void Initialize(HexMapPaintMode paintMode, int q, int r)
    {
        _paintMode = paintMode;
        _q = q;
        _r = r;
    }
}
