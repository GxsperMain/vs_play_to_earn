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
    private long lastTimestamp = 0;
    private ICoreServerAPI serverAPI;
    private readonly List<IServerPlayer> onlinePlayers = [];

    /// <summary>
    /// Stores all players wallets
    /// </summary>
    private Dictionary<string, string> playerWallets = [];
    /// <summary>
    /// Stores the actual quantity of coins per wallet
    /// </summary>
    private Dictionary<string, long> walletsValues = [];

    /// <summary>
    ///  Player UID / bool for earning coing
    /// </summary>
    public static readonly Dictionary<string, bool> playersWalletsStatus = [];

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
            .WithDescription("Set up your wallet address, /wallet 0x123... to start earning PTE")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithArgs(new StringArgParser("arguments", false))
            .HandleWith(SetWalletAddress);

        api.ChatCommands.Create("balance")
            .WithDescription("View your PTE balance to earn, ensure you have set your wallet using /wallet 0x123...")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(ViewBalance);

        api.Event.PlayerNowPlaying += PlayerJoin;
        api.Event.PlayerDisconnect += PlayerDisconnect;
    }

    #region commands
    private TextCommandResult ViewBalance(TextCommandCallingArgs args)
    {
        string statusText = "";
        if (playersWalletsStatus.TryGetValue(args.Caller.Player.PlayerUID, out bool status))
        {
            if (status) statusText = ", Currently earning PTE";
            else statusText = ", YOU ARE NOT EARNING PTE";
        }

        if (!playerWallets.TryGetValue(args.Caller.Player.PlayerUID, out _))
            return TextCommandResult.Error($"You don't have any wallet set up", "4");

        if (walletsValues.TryGetValue(playerWallets[args.Caller.Player.PlayerUID], out long balance))
            return TextCommandResult.Success($"PTE: {Math.Round(balance / (decimal)Math.Pow(10, 18), 2)}{statusText}", "3");
        else
            return TextCommandResult.Error($"You don't have any balance", "5");
    }

    private TextCommandResult SetWalletAddress(TextCommandCallingArgs args)
    {
        if (args[0] == null) return TextCommandResult.Error($"No wallet provided", "5");

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
            try
            {
                // No players? no usseless disk read/write
                if (onlinePlayers.Count == 0)
                {
                    if (Configuration.enableExtendedLog)
                        Debug.Log("No players online...");
                    lastTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    return;
                }



                long actualTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int secondsPassed = (int)(actualTimestamp - lastTimestamp);
                long additionalCoins = Configuration.coinsPerSecond * secondsPassed;

                // Directories creations
                Directory.CreateDirectory(Path.GetDirectoryName(Configuration.lockFile));
                Directory.CreateDirectory(Path.GetDirectoryName(Configuration.resyncFile));
                Directory.CreateDirectory(Path.GetDirectoryName(Configuration.walletsPath));
                Directory.CreateDirectory(Path.GetDirectoryName(Configuration.playersWalletsPath));

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
                foreach (IServerPlayer player in onlinePlayers)
                {
                    // Check if wallet was registered
                    if (playerWallets.TryGetValue(player.PlayerUID, out string walletAddress))
                    {
                        // Afk players
                        if (Events.playersSoftAfk.Contains(player.PlayerUID))
                        {
                            if (Configuration.enableExtendedLog)
                                Debug.Log("Ignoring " + player.PlayerName + " because he is afk");

                            playersWalletsStatus[player.PlayerUID] = false;
                            continue;
                        }

                        // Giving coins
                        if (walletsValues.TryGetValue(walletAddress, out _))
                            walletsValues[walletAddress] += additionalCoins;
                        else
                            walletsValues[walletAddress] = additionalCoins;

                        playersWalletsStatus[player.PlayerUID] = true;

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
            }
            catch (Exception ex)
            {
                Debug.Log($"ERROR: {ex.Message}");
            }
        });
    }
    #endregion

    #region login events
    private void PlayerJoin(IServerPlayer byPlayer)
    {
        onlinePlayers.Add(byPlayer);
    }
    private void PlayerDisconnect(IServerPlayer byPlayer)
    {
        onlinePlayers.Remove(byPlayer);
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
