using TMPro;
using UnityEngine;

public static class ShopSaleMark
{
    public const string ObjectName = "SaleMark";

    public static void Apply(Transform root, int percent)
    {
        Transform mark = FindNamedChild(root, ObjectName);
        if (mark == null)
            return;

        bool show = percent > 0;
        mark.gameObject.SetActive(show);
        if (!show)
            return;

        TMP_Text label = mark.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = $"-{percent}%";
    }

    private static Transform FindNamedChild(Transform root, string name)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child;

            Transform nested = FindNamedChild(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
