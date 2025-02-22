using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace PlayToEarn;

#pragma warning disable CA2211
public static class Configuration
{
    private static Dictionary<string, object> LoadConfigurationByDirectoryAndName(ICoreAPI api, string directory, string name, string defaultDirectory)
    {
        string directoryPath = Path.Combine(api.DataBasePath, directory);
        string configPath = Path.Combine(api.DataBasePath, directory, $"{name}.json");
        Dictionary<string, object> loadedConfig;
        try
        {
            // Load server configurations
            string jsonConfig = File.ReadAllText(configPath);
            loadedConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonConfig);
        }
        catch (DirectoryNotFoundException)
        {
            Initialization.Debug.Log($"WARNING: Server configurations directory does not exist creating {name}.json and directory...");
            try
            {
                Directory.CreateDirectory(directoryPath);
            }
            catch (Exception ex)
            {
                Initialization.Debug.Log($"ERROR: Cannot create directory: {ex.Message}");
            }
            Initialization.Debug.Log("Loading default configurations...");
            // Load default configurations
            loadedConfig = api.Assets.Get(new AssetLocation(defaultDirectory)).ToObject<Dictionary<string, object>>();

            // Creating default paths
            loadedConfig["playersWalletsPath"] = Path.Combine(api.DataBasePath, "Wallets", "players_wallets.json");
            loadedConfig["walletsPath"] = Path.Combine(api.DataBasePath, "Wallets", "wallets.json");
            loadedConfig["lockFile"] = Path.Combine(api.DataBasePath, "Wallets", "wallets.lock");
            loadedConfig["resyncFile"] = Path.Combine(api.DataBasePath, "Wallets", "wallets.resync");

            Initialization.Debug.Log($"Configurations loaded, saving configs in: {configPath}");
            try
            {
                // Saving default configurations
                string defaultJson = JsonConvert.SerializeObject(loadedConfig, Formatting.Indented);
                File.WriteAllText(configPath, defaultJson);
            }
            catch (Exception ex)
            {
                Initialization.Debug.Log($"ERROR: Cannot save default files to {configPath}, reason: {ex.Message}");
            }
        }
        catch (FileNotFoundException)
        {
            Initialization.Debug.Log($"WARNING: Server configurations {name}.json cannot be found, recreating file from default");
            Initialization.Debug.Log("Loading default configurations...");
            // Load default configurations
            loadedConfig = api.Assets.Get(new AssetLocation(defaultDirectory)).ToObject<Dictionary<string, object>>();

            Initialization.Debug.Log($"Configurations loaded, saving configs in: {configPath}");
            try
            {
                // Saving default configurations
                string defaultJson = JsonConvert.SerializeObject(loadedConfig, Formatting.Indented);
                File.WriteAllText(configPath, defaultJson);
            }
            catch (Exception ex)
            {
                Initialization.Debug.Log($"ERROR: Cannot save default files to {configPath}, reason: {ex.Message}");
            }

        }
        catch (Exception ex)
        {
            Initialization.Debug.Log($"ERROR: Cannot read the server configurations: {ex.Message}");
            Initialization.Debug.Log("Loading default values from mod assets...");
            // Load default configurations
            loadedConfig = api.Assets.Get(new AssetLocation(defaultDirectory)).ToObject<Dictionary<string, object>>();
        }
        return loadedConfig;
    }


    #region baseconfigs
    public static int millisecondsPerTick = 5000;
    #region Gameplay Earn
    public static bool earnByPlaying = true;
    public static BigInteger coinsPerSecond = 277777800000000;
    #endregion

    public static string playersWalletsPath = "";
    public static string walletsPath = "";
    public static string lockFile = "";
    public static string resyncFile = "";
    public static bool enableExtendedLog = false;

    public static void UpdateBaseConfigurations(ICoreAPI api)
    {
        Dictionary<string, object> baseConfigs = LoadConfigurationByDirectoryAndName(
            api,
            "ModConfig/PlayToEarn/config",
            "base",
            "playtoearn:config/base.json"
        );
        { //millisecondsPerTick
            if (baseConfigs.TryGetValue("millisecondsPerTick", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: millisecondsPerTick is null");
                else if (value is not long) Initialization.Debug.Log($"CONFIGURATION ERROR: millisecondsPerTick is not int is {value.GetType()}");
                else millisecondsPerTick = (int)(long)value;
            else Initialization.Debug.Log("CONFIGURATION ERROR: millisecondsPerTick not set");
        }
        { //earnByPlaying
            if (baseConfigs.TryGetValue("earnByPlaying", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: earnByPlaying is null");
                else if (value is not bool) Initialization.Debug.Log($"CONFIGURATION ERROR: earnByPlaying is not boolean is {value.GetType()}");
                else earnByPlaying = (bool)value;
            else Initialization.Debug.Log("CONFIGURATION ERROR: earnByPlaying not set");
        }
        { //coinsPerSecond
            if (baseConfigs.TryGetValue("coinsPerSecond", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: coinsPerSecond is null");
                else if (value is not BigInteger and not long) Initialization.Debug.Log($"CONFIGURATION ERROR: coinsPerSecond is not BigInteger is {value.GetType()}, {value}");
                else
                if (value.GetType() == typeof(BigInteger)) coinsPerSecond = (BigInteger)value;
                else coinsPerSecond = new BigInteger((long)value);
            else Initialization.Debug.Log("CONFIGURATION ERROR: coinsPerSecond not set");
        }
        { //playersWalletsPath
            if (baseConfigs.TryGetValue("playersWalletsPath", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: playersWalletsPath is null");
                else if (value is not string) Initialization.Debug.Log($"CONFIGURATION ERROR: playersWalletsPath is not int is {value.GetType()}");
                else playersWalletsPath = (string)value;
            else Initialization.Debug.Log("CONFIGURATION ERROR: playersWalletsPath not set");
        }
        { //walletsPath
            if (baseConfigs.TryGetValue("walletsPath", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: walletsPath is null");
                else if (value is not string) Initialization.Debug.Log($"CONFIGURATION ERROR: walletsPath is not int is {value.GetType()}");
                else walletsPath = (string)value;
            else Initialization.Debug.Log("CONFIGURATION ERROR: walletsPath not set");
        }
        { //lockFile
            if (baseConfigs.TryGetValue("lockFile", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: lockFile is null");
                else if (value is not string) Initialization.Debug.Log($"CONFIGURATION ERROR: lockFile is not int is {value.GetType()}");
                else lockFile = (string)value;
            else Initialization.Debug.Log("CONFIGURATION ERROR: lockFile not set");
        }
        { //resyncFile
            if (baseConfigs.TryGetValue("resyncFile", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: resyncFile is null");
                else if (value is not string) Initialization.Debug.Log($"CONFIGURATION ERROR: resyncFile is not int is {value.GetType()}");
                else resyncFile = (string)value;
            else Initialization.Debug.Log("CONFIGURATION ERROR: resyncFile not set");
        }
        { //enableExtendedLog
            if (baseConfigs.TryGetValue("enableExtendedLog", out object value))
                if (value is null) Initialization.Debug.Log("CONFIGURATION ERROR: enableExtendedLog is null");
                else if (value is not bool) Initialization.Debug.Log($"CONFIGURATION ERROR: enableExtendedLog is not boolean is {value.GetType()}");
                else enableExtendedLog = (bool)value;
            else Initialization.Debug.Log("CONFIGURATION ERROR: enableExtendedLog not set");
        }
    }

    public static string FormatCoinToHumanReadable(BigInteger quantity)
    {
        string quantityString = quantity.ToString();
        if (quantityString.Length <= 15) return "0.00";
        else quantityString = quantityString[..^15];

        if (quantityString.Length == 1) return $"0.0{quantityString}";
        if (quantityString.Length == 2) return $"0.{quantityString}";
        else return quantityString[..^3] + "," + quantityString[^2..];
    }
    #endregion
}