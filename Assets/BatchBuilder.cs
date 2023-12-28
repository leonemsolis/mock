#if UNITY_EDITOR
using System;
using System.IO;
using Cysharp.Threading.Tasks;
using ModestTree;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;

public class BatchBuilder
{
    private const string BUILD_SCRIPT = "Assets/AddressableAssetsData/DataBuilders/BuildScriptPackedMode.asset";
    private const string SETTINGS_ASSET = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
    
    private const string PROFILE_NAME_DEFAULT = "Yandex";


    private const string CATALOG_CHECKING_ERROR = "[-] CATALOGS ERROR";

    private static AddressableAssetSettings _settings;

    private static string _logFileName;

    private static string GetLogFileName()
    {
        var fullPath = Application.dataPath;
        var findedPath = fullPath.Split('/');
        var resultPath = findedPath[^2];
        return resultPath;
    }

    public static string GetProfileValue()
    {
        var commandLineArgs = Environment.GetCommandLineArgs();
        var index = commandLineArgs.IndexOf("-profile");
        var result = string.Empty;

        if (index > 0 && index < commandLineArgs.Length - 1)
            result = commandLineArgs[index + 1];
        
        return result;
    }
    
    public static void UpdatePreviousAddressablesBuild()
    {
        BuildAsync().Forget();
    }

    public static void ClearAndBuildAddressables()
    {
        BuildAsync(true).Forget();
    }

    private static async UniTask BuildAsync(bool cleanBuild = false)
    {
        try
        {
            PrepareLogFile();

            await WriteToLog("[+] Started building");

            if (cleanBuild)
            {
                await WriteToLog("[+] Selected build type: [Clean build]");

                await ClearCache();
            }
            else
            {
                await WriteToLog("[+] Selected build type: [Update previous build]");
            }

            await GetSettingsObject(SETTINGS_ASSET);

            await InitializeProfile();

            await WriteToLog($"[+] Getting builder script from: {BUILD_SCRIPT}");

            var builderScript = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BUILD_SCRIPT) as IDataBuilder;

            await WriteToLog("[+] Builder script succesfully imported");

            await SetBuilder(builderScript);

            await ClearPreviousCatalogs();

            await BuildAddressableContent();

            var catalogsResult = await IsCatalogsBuildedCorrect();
            if (!catalogsResult.Item1)
            {
                await WriteToLog($"[-] Finded catalogs count: {catalogsResult.Item2}");
                await WriteToLog(CATALOG_CHECKING_ERROR);
            }
            else
            {
                await WriteToLog("[+] Catalogs checking finished");
            }
        }
        catch (Exception ex)
        {
            if (string.IsNullOrEmpty(_logFileName))
                PrepareLogFile();

            await WriteToLog($"[-] Unexpected exception: {ex.Message}.\r\nPlease, check previous logs.");
            throw;
        }
    }

    private static async UniTask ClearCache()
    {
        await WriteToLog("[+] Started clear cache for prepare clean build");

        try
        {
            await WriteToLog("[+] Started clean player content");
            AddressableAssetSettings.CleanPlayerContent();
            await WriteToLog("[+] Player content successfully cleared");

            await WriteToLog("[+] Started clear cached data from ActivePlayerDataBuilder");
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder.ClearCachedData();
            await WriteToLog("[+] ActivePlayerDataBuilder successfully cleared");

            await WriteToLog("[+] Started purge cache from BuildCache");
            BuildCache.PurgeCache(false);
            await WriteToLog("[+] BuildCache successfully cleared");

            await WriteToLog("[+] Clean old bundles directory...");
            ClearOldBundlesDirectory();
            await WriteToLog("[+] Old bundles directory is cleared");

            await WriteToLog("[+] Clear addressables cache successfully finished");
        }
        catch (Exception ex)
        {
            await WriteToLog($"[-] Exception when clearing cache: {ex.Message}");
            throw;
        }
    }

    private static async UniTask GetSettingsObject(string settingsAsset)
    {
        await WriteToLog($"[+] Getting addressable settins from path: {settingsAsset}");

        _settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(settingsAsset) as AddressableAssetSettings;

        await WriteToLog("[+] Settings succesfully imported");
    }

    private static async UniTask InitializeProfile()
    {
        try
        {
            await WriteToLog($"[+] Getting profile from command line args...");

            var profile = GetProfileValue();
            if (string.IsNullOrEmpty(profile))
            {
                await WriteToLog($"[-] Can't find profile value in arguments. Default profile will be used with name: {PROFILE_NAME_DEFAULT}");
                profile = PROFILE_NAME_DEFAULT;
            }

            await WriteToLog($"[+] Find profileID for current profile name: {profile}");

            var profileId = _settings.profileSettings.GetProfileId(profile);
            if (string.IsNullOrEmpty(profileId))
            {
                await WriteToLog($"[-] Can't find profile ID for current profile name: {profile}");
                throw new Exception($"Can't find profile ID for current profile name: {profile}");
            }

            await WriteToLog($"[+] Finded ID for profile {profile}: {profileId}");

            _settings.activeProfileId = profileId;

            await WriteToLog($"[+] Profile {profile} successfully activated");
        }
        catch
        {
            await WriteToLog("[-] Exception when initialize profile. Please check input data.");
            throw;
        }
    }

    private static async UniTask SetBuilder(IDataBuilder builder)
    {
        await WriteToLog("[+] Preparing builder object");

        var index = _settings.DataBuilders.IndexOf((ScriptableObject)builder);

        await WriteToLog($"[+] Finded builder index in DataBuilders: {index}");

        if (index > 0)
        {
            _settings.ActivePlayerDataBuilderIndex = index;
            await WriteToLog("[+] Data builder index successfully activated");
        }
        else
        {
            await WriteToLog($"[-] EXCEPTION: Can't activated builder for index: {index}");
            await WriteToLog($"[-] {builder} must be added to the DataBuilders list before it can be made active");
        }
    }

    private static async UniTask BuildAddressableContent()
    {
        await WriteToLog("[+] Started building addressable content");

        AddressableAssetSettings.BuildPlayerContent(out var result);

        await WriteToLog("[+] Player content building finished");


        var success = string.IsNullOrEmpty(result.Error);
        if (success)
        {
            await WriteToLog("[+] Player content successfully builded");
            await WriteToLog("[+] BUILDER SCRIPT FINISHED: Success");
        }
        else
        {
            await WriteToLog($"[-] EXCEPTION: Can't finish building with error: {result.Error}");
            await WriteToLog("[-] BUILDER SCRIPT FINISHED: Exception");
        }
    }

    private static void PrepareLogFile()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "BuilderLogs",
            Directory.GetParent(Application.dataPath)!.Name);

        var fileName = GetLogFileName();
        fileName += ".txt";

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var finalPath = Path.Combine(directory, fileName);
        if (File.Exists(finalPath))
            File.Delete(finalPath);

        _logFileName = finalPath;
    }

    private static async UniTask WriteToLog(string text)
    {
        using (var writer = new StreamWriter(_logFileName, true))
        {
            await writer.WriteLineAsync(DateTime.Now.ToLongTimeString() + " --> " + text);
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

    private static async UniTask<(bool, int)> IsCatalogsBuildedCorrect()
    {
        var platformFolder = GetPlatformName();
        await WriteToLog($"[+] Started checking catalogs after build for platform: {platformFolder}");

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

    private static async UniTask ClearPreviousCatalogs()
    {
        var platformFolder = GetPlatformName();
        await WriteToLog($"[+] Started checking old catalogs for platform: {platformFolder}");

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
                await WriteToLog($"[-] Finded old catalog file: {file}");
                File.Delete(file);
                await WriteToLog($"[-] Deleted old catalog file: {file}");
            }
        }

        await WriteToLog("[+] Checking old catalogs finished");
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

#endif