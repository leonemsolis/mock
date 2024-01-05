using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using ModestTree;
using UnityEditor.Build.Pipeline.Utilities;

public class BatchBuild
{
    private const string BuildScript = "Assets/AddressableAssetsData/DataBuilders/BuildScriptPackedMode.asset";
    private const string SettingsAsset = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
    private const string ProfileNameDefault = "Yandex";
    private static AddressableAssetSettings _settings;

    private static string GetProfileValue()
    {
        var commandLineArgs = Environment.GetCommandLineArgs();
        var index = commandLineArgs.IndexOf("-profile");
        var result = string.Empty;

        if (index > 0 && index < commandLineArgs.Length - 1)
            result = commandLineArgs[index + 1];
        
        return result;
    }
    
    [MenuItem("Addressables/Update build")]
    public static void UpdatePreviousAddressablesBuild()
    {
        Build();
    }

    [MenuItem("Addressables/Clean Build (long)")]
    public static void ClearAndBuildAddressables()
    {
        Build(true);
    }

    private static void Build(bool cleanBuild = false)
    {
        try
        {
            if (cleanBuild)
            {
                Debug.Log("[+] Selected build type: [Clean build]");
                ClearCache();
            }
            else
            {
                Debug.Log("[+] Selected build type: [Update previous build]");
            }

            GetSettingsObject(SettingsAsset);

            InitializeProfile();

            Debug.Log($"[+] Getting builder script from: {BuildScript}");

            var builderScript = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BuildScript) as IDataBuilder;

            Debug.Log("[+] Builder script succesfully imported");

            SetBuilder(builderScript);

            ClearPreviousCatalogs();

            BuildAddressableContent();
        }
        catch (Exception ex)
        {
            Debug.Log($"[-] Unexpected exception: {ex.Message}.\r\nPlease, check previous logs.");
            throw;
        }
    }

    private static void ClearCache()
    {
        Debug.Log("[+] Started clear cache for prepare clean build");

        try
        {
            Debug.Log("[+] Started clean player content");
            AddressableAssetSettings.CleanPlayerContent();
            Debug.Log("[+] Player content successfully cleared");

            Debug.Log("[+] Started clear cached data from ActivePlayerDataBuilder");
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder.ClearCachedData();
            Debug.Log("[+] ActivePlayerDataBuilder successfully cleared");

            Debug.Log("[+] Started purge cache from BuildCache");
            BuildCache.PurgeCache(false);
            Debug.Log("[+] BuildCache successfully cleared");

            Debug.Log("[+] Clean old bundles directory...");
            ClearOldBundlesDirectory();
            Debug.Log("[+] Old bundles directory is cleared");

            Debug.Log("[+] Clear addressables cache successfully finished");
        }
        catch (Exception ex)
        {
            Debug.Log($"[-] Exception when clearing cache: {ex.Message}");
            throw;
        }
    }

    private static void GetSettingsObject(string settingsAsset)
    {
        Debug.Log($"[+] Getting addressable settins from path: {settingsAsset}");
        _settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(settingsAsset) as AddressableAssetSettings;
        Debug.Log("[+] Settings succesfully imported");
    }

    private static void InitializeProfile()
    {
        try
        {
            Debug.Log($"[+] Getting profile from command line args...");

            var profile = GetProfileValue();
            if (string.IsNullOrEmpty(profile))
            {
                Debug.Log($"[-] Can't find profile value in arguments. Default profile will be used with name: {ProfileNameDefault}");
                profile = ProfileNameDefault;
            }

            Debug.Log($"[+] Find profileID for current profile name: {profile}");

            var profileId = _settings.profileSettings.GetProfileId(profile);
            if (string.IsNullOrEmpty(profileId))
            {
                Debug.Log($"[-] Can't find profile ID for current profile name: {profile}");
                throw new Exception($"Can't find profile ID for current profile name: {profile}");
            }

            Debug.Log($"[+] Finded ID for profile {profile}: {profileId}");

            _settings.activeProfileId = profileId;

            Debug.Log($"[+] Profile {profile} successfully activated");
        }
        catch
        {
            Debug.Log("[-] Exception when initialize profile. Please check input data.");
            throw;
        }
    }

    private static void SetBuilder(IDataBuilder builder)
    {
        Debug.Log("[+] Preparing builder object");

        var index = _settings.DataBuilders.IndexOf((ScriptableObject)builder);

        Debug.Log($"[+] Finded builder index in DataBuilders: {index}");

        if (index > 0)
        {
            _settings.ActivePlayerDataBuilderIndex = index;
            Debug.Log("[+] Data builder index successfully activated");
        }
        else
        {
            Debug.Log($"[-] EXCEPTION: Can't activated builder for index: {index}");
            Debug.Log($"[-] {builder} must be added to the DataBuilders list before it can be made active");
        }
    }

    private static void BuildAddressableContent()
    {
        Debug.Log("[+] Started building addressable content");

        AddressableAssetSettings.BuildPlayerContent(out var result);

        Debug.Log("[+] Player content building finished");


        if (result is null)
        {
            Debug.Log("[-] ERROR: Result of build operation is not defined");
            Debug.Log("[-] BUILDER SCRIPT FINISHED: Exception");
            throw new Exception("Result of build operation is not defined");
        }
        
        var success = string.IsNullOrEmpty(result.Error);
        if (success)
        {
            Debug.Log("[+] Player content successfully builded");
            Debug.Log("[+] BUILDER SCRIPT FINISHED: Success");
            
            var catalogsResult = IsCatalogsBuildedCorrect();
            if (!catalogsResult.Item1)
            {
                Debug.Log($"[-] Finded catalogs count: {catalogsResult.Item2}");
                Debug.Log("[-] CATALOGS ERROR");
                throw new Exception($"Finded catalogs count: {catalogsResult.Item2}");
            }
            Debug.Log("[+] Catalogs checking finished");
            PostBuild(result);
        }
        else
        {
            Debug.Log($"[-] EXCEPTION: Can't finish building with error: {result.Error}");
            Debug.Log("[-] BUILDER SCRIPT FINISHED: Exception");
            throw new Exception(result.Error);
        }
    }

    private static void ClearOldBundlesDirectory()
    {
        var platformFolder = GetPlatformName();
        if (string.IsNullOrEmpty(platformFolder))
            return;

        var serverDataPath = Path.Combine(Directory.GetCurrentDirectory(), "ServerData", platformFolder);
        if (Directory.Exists(serverDataPath))
            Directory.Delete(serverDataPath, true);
    }

    private static (bool, int) IsCatalogsBuildedCorrect()
    {
        var platformFolder = GetPlatformName();
        Debug.Log($"[+] Started checking catalogs after build for platform: {platformFolder}");

        if (string.IsNullOrEmpty(platformFolder))
            return (false, 0);

        var serverDataPath = Path.Combine(Directory.GetCurrentDirectory(), "ServerData", platformFolder);

        if (!Directory.Exists(serverDataPath))
            return (false, 0);

        var catalogsCount = 0;
        foreach (var file in Directory.GetFiles(serverDataPath))
        {
            var extension = Path.GetExtension(file);
            if (string.IsNullOrEmpty(extension))
                continue;

            if (extension.ToLower().Contains("json") || extension.ToLower().Contains("hash"))
                catalogsCount++;
        }

        return (catalogsCount == 2, catalogsCount);
    }
    
    private static void PostBuild(AddressablesPlayerBuildResult result)
    {
        var bundlePath = Path.GetDirectoryName(Application.dataPath) +
                         $"/ServerData/{EditorUserBuildSettings.activeBuildTarget}";
        var files = Directory.GetFiles(bundlePath);
        var bundlesInCatalog = result.FileRegistry.GetFilePaths().Select(Path.GetFileName).ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!bundlesInCatalog.Contains(fileName) || fileName.EndsWith(".hash"))
            {
                Debug.Log($"Deleting file {file}");
                File.Delete(file);
            }
        }

        files = Directory.GetFiles(bundlePath);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.EndsWith(".json"))
            {
                Debug.Log($"Renaming catalog {file}");
                File.Move(file, Path.GetDirectoryName(file) + "/catalog_bundle.json");
            }
        }
    }

    private static void ClearPreviousCatalogs()
    {
        var platformFolder = GetPlatformName();
        Debug.Log($"[+] Started checking old catalogs for platform: {platformFolder}");

        if (string.IsNullOrEmpty(platformFolder))
            return;

        var serverDataPath = Path.Combine(Directory.GetCurrentDirectory(), "ServerData", platformFolder);

        if (!Directory.Exists(serverDataPath))
            return;

        foreach (var file in Directory.GetFiles(serverDataPath))
        {
            var extension = Path.GetExtension(file);
            if (string.IsNullOrEmpty(extension))
                continue;

            if (extension.ToLower().Contains("json") || extension.ToLower().Contains("hash"))
            {
                Debug.Log($"[-] Finded old catalog file: {file}");
                File.Delete(file);
                Debug.Log($"[-] Deleted old catalog file: {file}");
            }
        }

        Debug.Log("[+] Checking old catalogs finished");
    }

    private static string GetPlatformName()
    {
#if UNITY_STANDALONE_WIN
        return "StandaloneWindows64";
#elif UNITY_STANDALONE_OSX
        return "StandaloneOSX";
#elif UNITY_ANDROID
        return "Android";
#elif UNITY_IOS
        return "iOS";
#else
        return null;
#endif
    }
}