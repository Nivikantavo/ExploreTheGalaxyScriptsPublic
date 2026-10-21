using UnityEngine;

public static class HexCoordinates
{
    public const float Sqrt3 = 1.7320508075688772f;

    public static Vector3 AxialToWorld(int q, int r, float hexSize, float y = 0f)
    {
        float x = hexSize * (Sqrt3 * q + Sqrt3 * 0.5f * r);
        float z = hexSize * 1.5f * r;
        return new Vector3(x, y, z);
    }

    public static Vector2Int WorldToAxial(Vector3 worldPos, float hexSize)
    {
        if (hexSize <= 0f)
            return Vector2Int.zero;

        float q = (Sqrt3 / 3f * worldPos.x - 1f / 3f * worldPos.z) / hexSize;
        float r = (2f / 3f * worldPos.z) / hexSize;
        return RoundAxial(q, r);
    }

    public static Vector2Int RoundAxial(float q, float r)
    {
        float s = -q - r;
        int rq = Mathf.RoundToInt(q);
        int rr = Mathf.RoundToInt(r);
        int rs = Mathf.RoundToInt(s);

        float qDiff = Mathf.Abs(rq - q);
        float rDiff = Mathf.Abs(rr - r);
        float sDiff = Mathf.Abs(rs - s);

        if (qDiff > rDiff && qDiff > sDiff)
            rq = -rr - rs;
        else if (rDiff > sDiff)
            rr = -rq - rs;

        return new Vector2Int(rq, rr);
    }

    public static void GetHexCorners(Vector3 center, float hexSize, Vector3[] corners)
    {
        for (int i = 0; i < 6; i++)
        {
            float angle = 60f * i * Mathf.Deg2Rad;
            corners[i] = center + new Vector3(
                hexSize * Mathf.Sin(angle),
                0f,
                hexSize * Mathf.Cos(angle));
        }
    }
}
