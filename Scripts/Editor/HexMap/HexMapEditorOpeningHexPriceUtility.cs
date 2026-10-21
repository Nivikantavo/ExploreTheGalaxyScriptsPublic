using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[Serializable]
public struct OpeningHexPriceEntry
{
    public ResourceType ResourceType;
    public int Amount;
}

public static class HexMapEditorOpeningHexPriceUtility
{
    public static void DrawPriceEditor(List<OpeningHexPriceEntry> entries)
    {
        EditorGUILayout.LabelField("Цена открытия", EditorStyles.boldLabel);

        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox("Добавьте хотя бы один ресурс и его количество.", MessageType.Info);
        }

        int removeIndex = -1;

        for (int i = 0; i < entries.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            var entry = entries[i];
            entry.ResourceType = (ResourceType)EditorGUILayout.EnumPopup(entry.ResourceType);
            entry.Amount = EditorGUILayout.IntField(entry.Amount, GUILayout.Width(72));
            entries[i] = entry;

            if (GUILayout.Button("-", GUILayout.Width(22f)))
                removeIndex = i;

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            entries.RemoveAt(removeIndex);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(entries.Count >= Enum.GetValues(typeof(ResourceType)).Length))
        {
            if (GUILayout.Button("Добавить ресурс", GUILayout.Width(140f)))
                entries.Add(CreateDefaultEntry(entries));
        }
        EditorGUILayout.EndHorizontal();
    }

    public static bool TryBuildPriceDictionary(
        List<OpeningHexPriceEntry> entries,
        out Dictionary<ResourceType, int> price,
        out string error)
    {
        price = new Dictionary<ResourceType, int>();
        error = null;

        if (entries == null || entries.Count == 0)
        {
            error = "Укажите цену открытия: добавьте хотя бы один ресурс.";
            return false;
        }

        var usedTypes = new HashSet<ResourceType>();

        foreach (var entry in entries)
        {
            if (entry.Amount <= 0)
            {
                error = $"Количество для {entry.ResourceType} должно быть больше 0.";
                return false;
            }

            if (!usedTypes.Add(entry.ResourceType))
            {
                error = $"Ресурс {entry.ResourceType} указан несколько раз.";
                return false;
            }

            price[entry.ResourceType] = entry.Amount;
        }

        return true;
    }

    public static void ApplyPrice(SerializedObject serializedObject, IReadOnlyDictionary<ResourceType, int> price)
    {
        var priceProperty = serializedObject.FindProperty("_price");
        if (priceProperty == null)
            return;

        var listProperty = priceProperty.FindPropertyRelative("_serializedList");
        listProperty.ClearArray();

        int index = 0;
        foreach (var entry in price)
        {
            listProperty.InsertArrayElementAtIndex(index);
            var element = listProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("Key").enumValueIndex = (int)entry.Key;
            element.FindPropertyRelative("Value").intValue = entry.Value;
            index++;
        }
    }

    private static OpeningHexPriceEntry CreateDefaultEntry(List<OpeningHexPriceEntry> entries)
    {
        var usedTypes = entries.Select(entry => entry.ResourceType).ToHashSet();

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (!usedTypes.Contains(type))
                return new OpeningHexPriceEntry { ResourceType = type, Amount = 1 };
        }

        return new OpeningHexPriceEntry { ResourceType = ResourceType.Wood, Amount = 1 };
    }
}
