#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class BatchBuilder2
{
    private const string customUrlPathOper = "https://mroqed.s3-eu-west-1.amazonaws.com/28/bundle";
    private const string customUrlPathAnim = "https://mroqed.s3-eu-west-1.amazonaws.com/27/bundle";
    private const string customUrlPathStruc = "https://mroqed.s3-eu-west-1.amazonaws.com/9/bundle";

    public static void BuildContent() {

    if (!EntryValidator()) return;

       AddressableAssetSettings.BuildPlayerContent(out var result);

       if (result == null) throw new Exception("Addressable Build Error (undefined)");
       if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);

       ClearOldBundles(result);
    }

    public static void UpdateContent() {

    if (!EntryValidator()) return;

       var path = ContentUpdateScript.GetContentStateDataPath(false);
       AddressablesPlayerBuildResult result = null;
       
       if (!string.IsNullOrEmpty(path))
           result = ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, path);
       
       if (result == null) throw new Exception("Addressable Build Error (undefined)");
       if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);

       ClearOldBundles(result);
    }

    [MenuItem("Addressables/Custom Build Content")]
    public static void BuildOrUpdateContent() {

    if (!EntryValidator()) return;

       var path = ContentUpdateScript.GetContentStateDataPath(false);
       AddressablesPlayerBuildResult result = null;
       
       if (!File.Exists(path))
       {
           AddressableAssetSettings.BuildPlayerContent(out result);
       } else if (!string.IsNullOrEmpty(path))
           result = ContentUpdateScript.BuildContentUpdate(AddressableAssetSettingsDefaultObject.Settings, path);
       
       if (result == null) throw new Exception("Addressable Build Error (undefined))");
       if (!string.IsNullOrEmpty(result.Error)) throw new Exception(result.Error);

       ClearOldBundles(result);
    }

    private static bool EntryValidator()
    {
        return true;
       var groups = AddressableAssetSettingsDefaultObject.Settings.groups;
       foreach (var group in groups)
       {
           foreach (var entry in group.entries)
           {
               var isValid = Guid.TryParse(entry.address, out _);
               if (!isValid)
                   throw new Exception("Addressable Item Name is not matching GUID style");
               
               if (entry.address.Contains("/"))
                   throw new Exception("Addressable name contains slash sign");
                   
               if (entry.IsFolder || entry.IsScene)
                   throw new Exception("Addressable contains Folder or Scene, which is not allowed");
                   
               if (!entry.AssetPath.EndsWith(".prefab"))
                   throw new Exception("Assets type allowed: .prefab");
           }
       }

       return true;
    }

    private static void ClearOldBundles(AddressablesPlayerBuildResult result)
    {
       var bundlePath = Path.GetDirectoryName(Application.dataPath) + $"/ServerData/{EditorUserBuildSettings.activeBuildTarget}";
       var files = Directory.GetFiles(bundlePath);
       var bundlesInCatalog = result.FileRegistry.GetFilePaths().Select(Path.GetFileName).ToList();

       foreach (var file in files)
       {
           var fileName = Path.GetFileName(file);
           if(!bundlesInCatalog.Contains(fileName) || fileName.EndsWith(".hash")) File.Delete(file);
       }

       files = Directory.GetFiles(bundlePath);
       foreach (var file in files)
       {
           var fileName = Path.GetFileName(file);
           if(fileName.EndsWith(".json")) File.Move(file, Path.GetDirectoryName(file) + "/catalog_bundle.json");
       }

       SetCustomUrlPath();
    }

    private static void SetCustomUrlPath()
    {
       if(string.IsNullOrEmpty(customUrlPathOper) || string.IsNullOrEmpty(customUrlPathAnim) || string.IsNullOrEmpty(customUrlPathStruc)) return;
       
       var catalogPath = Path.GetDirectoryName(Application.dataPath) + $"/ServerData/{EditorUserBuildSettings.activeBuildTarget}/catalog_bundle.json";
       dynamic json = JsonConvert.DeserializeObject(File.ReadAllText(catalogPath));
       JArray items = json?.m_InternalIds;

       for (var i = 0; i < items?.Count; i++)
       {
           var strItem = items[i].ToString();
           if(!strItem.StartsWith("https")) continue;
           var filename = Path.GetFileName(strItem);
           var customUrlPath = filename.StartsWith("coursesoperations") 
               ? customUrlPathOper 
               : filename.StartsWith("coursesanimations") 
                   ? customUrlPathAnim 
                   : customUrlPathStruc;
           var relativePath = strItem.Split(new string[] { EditorUserBuildSettings.activeBuildTarget.ToString() }, StringSplitOptions.None);
           items[i] = $"{customUrlPath}/{EditorUserBuildSettings.activeBuildTarget}{relativePath[1]}";
       }

       File.WriteAllText(catalogPath, JsonConvert.SerializeObject(json));
    } 
}
#endif