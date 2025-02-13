using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AFKModule.Modules;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PlayToEarn;

public class PlayToEarnModSystem : ModSystem
{
    long lastTimestamp = 0;
    ICoreServerAPI serverAPI;

    Dictionary<string, string> playerWallets = [];
    Dictionary<string, long> walletsValues = [];

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        Debug.LoadLogger(api.Logger);
        Configuration.UpdateBaseConfigurations(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        if (File.Exists(Configuration.lockFile))
        {
            Debug.Log("ERROR: lock file is present cannot load the wallets file");
            api.Server.ShutDown();
        }

        if (File.Exists(Configuration.walletsPath))
        {
            // Locking the file because we are reading now
            using var lockFile = File.Create(Configuration.lockFile);
            lockFile.Close();

            string jsonContent = File.ReadAllText(Configuration.walletsPath);
            walletsValues = JsonConvert.DeserializeObject<Dictionary<string, long>>(jsonContent);

            File.Delete(Configuration.lockFile);
        }
        else Debug.Log($"ERROR: cannot read {Configuration.walletsPath} because its not exist, will be created any empty one");

        if (File.Exists(Configuration.playersWalletsPath))
        {
            string jsonContent = File.ReadAllText(Configuration.playersWalletsPath);
            playerWallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
        }

        serverAPI = api;
        api.Event.RegisterGameTickListener(OnTick, Configuration.millisecondsPerTick);

        api.ChatCommands.Create("wallet")
            .WithDescription("Set up your wallet address")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithArgs(new StringArgParser("arguments", false))
            .HandleWith(SetWalletAddress);

        api.ChatCommands.Create("balance")
            .WithDescription("View your PTE balance to earn")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(ViewBalance);
    }

    #region commands
    private TextCommandResult ViewBalance(TextCommandCallingArgs args)
    {
        if (walletsValues.TryGetValue(args.Caller.Player.PlayerUID, out long balance))
            return TextCommandResult.Success($"PTE: {balance / (decimal)Math.Pow(10, 18)}", "3");
        else
            return TextCommandResult.Error($"You don't have any balance or does not have any wallet set up", "4");
    }

    private TextCommandResult SetWalletAddress(TextCommandCallingArgs args)
    {
        static bool ValidAddress(string address)
            => Regex.IsMatch(address, @"^0x[a-fA-F0-9]{40}$");

        IPlayer player = args.Caller.Player;
        string address = args[0].ToString();

        if (!ValidAddress(address))
            return TextCommandResult.Error($"Invalid wallet", "1");

        playerWallets[player.PlayerUID] = address;
        File.WriteAllTextAsync(Configuration.playersWalletsPath, JsonConvert.SerializeObject(playerWallets, Formatting.Indented));

        return TextCommandResult.Success($"Wallet Set!", "2");
    }
    #endregion

    #region coin give per gameplay
    private void OnTick(float obj)
    {
        // Running on secondary thread to not struggle the server
        Task.Run(() =>
        {
            // No players? no usseless disk read/write
            if (serverAPI.World.AllOnlinePlayers.Length == 0)
            {
                lastTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return;
            }

            long actualTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int secondsPassed = (int)(actualTimestamp - lastTimestamp);
            long additionalCoins = Configuration.coinsPerSecond * secondsPassed;

            if (File.Exists(Configuration.lockFile))
            {
                Debug.Log($"WARNING: Wallet is busy...");
                return;
            }

            // Reading wallets json            
            if (File.Exists(Configuration.walletsPath) && File.Exists(Configuration.resyncFile))
            {
                if (Configuration.enableExtendedLog)
                    Debug.Log("Resync file is present, loading wallets values from disk to memory");

                // Locking the file because we are reading now
                using var lockFile = File.Create(Configuration.lockFile);
                lockFile.Close();

                string jsonContent = File.ReadAllText(Configuration.walletsPath);
                walletsValues = JsonConvert.DeserializeObject<Dictionary<string, long>>(jsonContent);

                File.Delete(Configuration.lockFile);
                File.Delete(Configuration.resyncFile);
            }

            // Adding additionalCoins to the wallets address
            foreach (IPlayer player in serverAPI.World.AllOnlinePlayers)
            {
                // Check if wallet was registered
                if (playerWallets.TryGetValue(player.PlayerUID, out string walletAddress))
                {
                    // Afk players
                    if (Events.playersSoftAfk.Contains(player.PlayerUID))
                    {
                        if (Configuration.enableExtendedLog)
                            Debug.Log("Ignoring " + player.PlayerName + " because he is afk");
                        continue;
                    }

                    // Giving coins
                    if (walletsValues.TryGetValue(walletAddress, out _))
                        walletsValues[walletAddress] += additionalCoins;
                    else
                        walletsValues[walletAddress] = additionalCoins;

                    if (Configuration.enableExtendedLog)
                        Debug.Log($"{player.PlayerName} have: {walletsValues[walletAddress] / (decimal)Math.Pow(10, 18)} PTE");
                }
            }

            lastTimestamp = actualTimestamp;

            // After calculations checks
            if (!File.Exists(Configuration.lockFile))
            {
                if (!File.Exists(Configuration.resyncFile))
                {
                    // Locking the file because we are reading now
                    using var lockFile = File.Create(Configuration.lockFile);
                    lockFile.Close();

                    File.WriteAllText(Configuration.walletsPath, JsonConvert.SerializeObject(walletsValues, Formatting.Indented));
                    File.Delete(Configuration.lockFile);
                }
                else Debug.Log($"ERROR: Cannot write the wallet values from memory to the {Configuration.walletsPath} a resync request was called");
            }
            else Debug.Log($"ERROR: Cannot write the wallet values from memory to the {Configuration.walletsPath} the file is busy");
        });
    }
    #endregion
    static public class Debug
    {
        static private ILogger loggerForNonTerminalUsers;
        static public void LoadLogger(ILogger logger) => loggerForNonTerminalUsers = logger;
        static public void Log(string message)
            => loggerForNonTerminalUsers?.Log(EnumLogType.Debug, $"[PlayToEarn] {message}");
    }

}
