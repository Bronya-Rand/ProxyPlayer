using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ProxyPlayer.Media;
using ProxyPlayer.Shared;
using ProxyPlayer.Utility;
using ProxyPlayer.Windows;

namespace ProxyPlayer;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/pplayer";

    public Configuration Configuration { get; init; }
    public PipeClient PipeClient { get; init; }
    public ProxyProcessManager ProxyProcessManager { get; init; }
    public DTRDisplay DtrDisplay { get; init; }

    public readonly WindowSystem WindowSystem = new(Constants.PluginName);
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        PipeClient = new PipeClient();
        ProxyProcessManager = new ProxyProcessManager();
        DtrDisplay = new DTRDisplay(this, PipeClient);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, PipeClient);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = $"""
            Opens the ProxyPlayer main window.
            {CommandName} toggle - Toggles play/pause for the current music source.
            {CommandName} next - Skips to the next track in the current music source.
            {CommandName} prev - Returns to the previous track in the current music source.
            {CommandName} stop - Stops playback for the current music source (if supported).
            {CommandName} shuffle - Toggles shuffle mode for the current music source (if supported).
            {CommandName} repeat - Toggles repeat mode for the current music source (if supported).
            {CommandName} songinfo - Displays the current track information for the active music source.
            {CommandName} next-source - Switches to the next available music source (if multiple sessions are available).
            """
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += DtrDisplay.UpdateDtr;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        Framework.Update -= DtrDisplay.UpdateDtr;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        DtrDisplay.Dispose();

        ProxyProcessManager.Dispose();
        PipeClient.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private static void PrintChatError(string message) => ChatGui.PrintError($"[{Constants.PluginName}] {message}");
    private void OnProxyCommand(string command, string args)
    {
        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        var subCommand = parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : string.Empty;

        if (!PipeClient.IsConnected)
        {
            ChatGui.PrintError($"[{Constants.PluginName}] Not connected to the ProxyPlayer server.");
            return;
        }
        var state = PipeClient.CurrentState;
        if (state.SelectedAppId == null)
        {
            ChatGui.PrintError($"[{Constants.PluginName}] No active music source selected.");
            return;
        }

        switch (subCommand)
        {
            // Play/Pause
            case "toggle":
                _ = PipeClient.SendCommandAsync(MediaCommand.PlayPause);
                break;
            case "next":
                _ = PipeClient.SendCommandAsync(MediaCommand.Next);
                break;
            case "prev":
                _ = PipeClient.SendCommandAsync(MediaCommand.Previous);
                break;
            case "stop":
                if (!state.SupportsStop)
                {
                    PrintChatError("The current music source does not support stopping playback.");
                    return;
                }
                _ = PipeClient.SendCommandAsync(MediaCommand.Stop);
                break;
            case "shuffle":
                if (!state.SupportsShuffling)
                {
                    PrintChatError("The current music source does not support shuffling.");
                    return;
                }
                _ = PipeClient.SendCommandAsync(MediaCommand.ToggleShuffle);
                break;
            case "repeat":
                if (!state.SupportsRepeat)
                {
                    PrintChatError("The current music source does not support repeating.");
                    return;
                }
                _ = PipeClient.SendCommandAsync(MediaCommand.ToggleRepeat);
                break;
            case "songinfo":
                var appName = TrackMetadata.GetFriendlyAppName(state);
                ChatGui.Print($"[{Constants.PluginName}] Now playing on {appName}: {state.Title} by {state.Artist}");
                break;
            case "next-source":
                if (state.AvailableAppIds.Length <= 1)
                {
                    PrintChatError("No other music source available to switch to.");
                    return;
                }
                
                // Find the next session
                var currentIndex = state.AvailableAppIds.IndexOf(state.SelectedAppId);
                var nextIndex = (currentIndex + 1) % state.AvailableAppIds.Length;
                var nextAppId = state.AvailableAppIds[nextIndex];

                // If the current session is still playing, pause it before switching
                if (state.PlaybackStatus != "Stopped" && state.PlaybackStatus != "Paused")
                {
                    _ = PipeClient.SendCommandAsync(MediaCommand.PlayPause);
                }

                // Switch and play the next session
                _ = PipeClient.SendCommandAsync(MediaCommand.SelectSession, nextAppId);
                _ = PipeClient.SendCommandAsync(MediaCommand.PlayPause);

                var friendlyNextAppName = TrackMetadata.GetFriendlyAppName(state);
                ChatGui.Print($"[{Constants.PluginName}] Switched to next music source: {friendlyNextAppName}");
                break;
            default:
                PrintChatError("Unknown command: {subCommand}");
                break;
        }
    }
    private void OnCommand(string command, string args)
    {
        var trimmedArgs = args.Trim();
        if (string.IsNullOrWhiteSpace(trimmedArgs))
        {
            ToggleMainUi();
            return;
        }
        OnProxyCommand(command, trimmedArgs);
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
