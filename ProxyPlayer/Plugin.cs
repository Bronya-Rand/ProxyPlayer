using System;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ProxyPlayer.Helpers;
using ProxyPlayer.Media;
using ProxyPlayer.Media.MPRIS;
using ProxyPlayer.Media.SMTC;
using ProxyPlayer.Models;
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
    public IMediaSource MediaSource { get; init; } = null!;
    public ProxyProcessManager? ProxyProcessManager { get; init; }
    public DTRDisplay DtrDisplay { get; init; } = null!;

    public readonly WindowSystem WindowSystem = new(Constants.PluginName);
    private ConfigWindow ConfigWindow { get; init; } = null!;
    private MainWindow MainWindow { get; init; } = null!;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (Util.IsWine())
            if (!AfUnixHelper.IsAfUnixSupported())
            {
                var notification = NotificationManager.AddNotification(new Notification
                {
                    Title = "Unsupported Wine/Proton Build",
                    Content = Constants.UnsupportedWineBuild,
                    Type = NotificationType.Error
                });
                return;
            }
            else
                MediaSource = new MprisMediaSource();
        else
        {
            ProxyProcessManager = new ProxyProcessManager();
            MediaSource = new SMTCMediaSource();
        }

        DtrDisplay = new DTRDisplay(this, MediaSource);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, MediaSource);

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

        MediaSource.Dispose();
        ProxyProcessManager?.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private static void PrintChatError(string message) => ChatGui.PrintError($"[{Constants.PluginName}] {message}");
    private void OnProxyCommand(string command, string args)
    {
        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        var subCommand = parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : string.Empty;

        if (!MediaSource.IsConnected)
        {
            var disconnectedText = MediaSource.SourceName == "Windows SMTC"
                ? "Not connected to the ProxyPlayer server."
                : $"Not connected to a {MediaSource.SourceName} session.";
            ChatGui.PrintError($"[{Constants.PluginName}] {disconnectedText}");
            return;
        }
        var state = MediaSource.CurrentState;
        if (state.SelectedAppId == null)
        {
            ChatGui.PrintError($"[{Constants.PluginName}] No active music source selected.");
            return;
        }

        switch (subCommand)
        {
            // Play/Pause
            case "toggle":
                _ = MediaSource.PlayPauseAsync();
                break;
            case "next":
                _ = MediaSource.NextAsync();
                break;
            case "prev":
                _ = MediaSource.PreviousAsync();
                break;
            case "stop":
                if (!state.SupportsStop)
                {
                    PrintChatError("The current music source does not support stopping playback.");
                    return;
                }
                _ = MediaSource.StopAsync();
                break;
            case "shuffle":
                if (!state.SupportsShuffling)
                {
                    PrintChatError("The current music source does not support shuffling.");
                    return;
                }
                _ = MediaSource.ToggleShuffleAsync();
                break;
            case "repeat":
                if (!state.SupportsRepeat)
                {
                    PrintChatError("The current music source does not support repeating.");
                    return;
                }
                _ = MediaSource.ToggleRepeatAsync();
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
                    _ = MediaSource.PlayPauseAsync();
                }

                // Switch and play the next session
                _ = MediaSource.SelectSessionAsync(nextAppId);
                _ = MediaSource.PlayPauseAsync();

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
