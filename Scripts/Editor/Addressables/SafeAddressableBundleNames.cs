#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SafeAddressableBundleNames
{
    public static void Apply(AddressableAssetSettings settings)
    {
        if (settings == null)
            return;

        List<string> changedGroups = null;
        List<AddressableAssetGroup> groups = settings.groups;
        for (int i = 0; i < groups.Count; i++)
        {
            AddressableAssetGroup group = groups[i];
            if (group == null || !GroupNameHasInvalidBundleCharacters(group.Name))
                continue;

            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                continue;

            bool changed = false;
            if (schema.UseDefaultSchemaSettings)
            {
                schema.UseDefaultSchemaSettings = false;
                changed = true;
            }

            if (schema.BundleNaming != BundledAssetGroupSchema.BundleNamingStyle.FileNameHash)
            {
                schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.FileNameHash;
                changed = true;
            }

            if (!changed)
                continue;

            changedGroups ??= new List<string>();
            changedGroups.Add(group.Name);
        }

        if (changedGroups == null)
            return;

        AssetDatabase.SaveAssets();
        Debug.Log("SafeAddressableBundleNames: File Name Hash set for " + string.Join(", ", changedGroups));
    }

    private static bool GroupNameHasInvalidBundleCharacters(string groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return false;

        for (int i = 0; i < groupName.Length; i++)
        {
            char character = groupName[i];
            bool allowed = (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character == '.'
                || character == '_'
                || character == '-';
            if (!allowed)
                return true;
        }

        return false;
    }
}

public sealed class SafeAddressableBundleNameBuildProcessor : IPreprocessBuildWithReport
{
    // AddressablesPlayerBuildProcessor.callbackOrder is 1. This callback runs first:
    // IPreprocessBuildWithReport is invoked before BuildPlayerProcessor.PrepareForBuild.
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        SafeAddressableBundleNames.Apply(AddressableAssetSettingsDefaultObject.Settings);
    }
}

public sealed class SafeAddressableBundleNameBuildMenu : AddressablesBuildMenuNewBuild
{
    public override bool OnPrebuild(AddressablesDataBuilderInput input)
    {
        AddressableAssetSettings settings = input != null ? input.AddressableSettings : null;
        SafeAddressableBundleNames.Apply(settings != null
            ? settings
            : AddressableAssetSettingsDefaultObject.Settings);
        return base.OnPrebuild(input);
    }
}
#endif
