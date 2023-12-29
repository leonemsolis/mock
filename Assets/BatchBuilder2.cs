#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class BatchBuilder2
{
    public static void BuildContent()
    {
        AddressableAssetSettings.BuildPlayerContent(out var result);

        if (result == null) throw new Exception("Addressable Build Error (undefined)");
        if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);

        ClearOldBundles(result);
    }

    public static void UpdateContent()
    {
        var path = ContentUpdateScript.GetContentStateDataPath(false);
        AddressablesPlayerBuildResult result = null;

        if (!string.IsNullOrEmpty(path))
            result = ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, path);

        if (result == null) throw new Exception("Addressable Build Error (undefined)");
        if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);

        ClearOldBundles(result);
    }

    [MenuItem("Addressables/Custom Build Content")]
    public static void BuildOrUpdateContent()
    {
        var path = ContentUpdateScript.GetContentStateDataPath(false);
        AddressablesPlayerBuildResult result = null;

        if (!File.Exists(path))
        {
            AddressableAssetSettings.BuildPlayerContent(out result);
        }
        else if (!string.IsNullOrEmpty(path))
            result = ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, path);

        if (result == null) throw new Exception("Addressable Build Error (undefined))");
        if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);

        ClearOldBundles(result);
    }

    private static void ClearOldBundles(AddressablesPlayerBuildResult result)
    {
        var bundlePath = Path.GetDirectoryName(Application.dataPath) +
                         $"/ServerData/{EditorUserBuildSettings.activeBuildTarget}";
        var files = Directory.GetFiles(bundlePath);
        var bundlesInCatalog = result.FileRegistry.GetFilePaths().Select(Path.GetFileName).ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!bundlesInCatalog.Contains(fileName) || fileName.EndsWith(".hash")) File.Delete(file);
        }

        files = Directory.GetFiles(bundlePath);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".json")) File.Move(file, Path.GetDirectoryName(file) + "/catalog_bundle.json");
        }
    }
}
#endif