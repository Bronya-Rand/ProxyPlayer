using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ProxyPlayer.Media;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;
using ProxyPlayer.Windows.Layout;

namespace ProxyPlayer.Windows;

public class MainWindow : Window, IDisposable
{
    private const string NoSessionSelectedText = "No media session selected.";

    private readonly Plugin plugin;
    private readonly IMediaSource mediaSource;
    private readonly TextureCache textures;
    private readonly SessionListModal sessionListModal;

    private readonly CompactLayout compactLayout;
    private readonly PortraitLayout portraitLayout;

    public MainWindow(Plugin plugin, IMediaSource mediaSource)
        : base("ProxyPlayer - Now Playing##ProxyPlayerNowPlaying", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        SizeCondition = ImGuiCond.FirstUseEver;

        this.mediaSource = mediaSource;
        textures = new TextureCache();
        this.plugin = plugin;

        sessionListModal = new SessionListModal(mediaSource)
        {
            OnSelectSessionId = sessionId => _ =
                mediaSource.SelectSessionAsync(sessionId)
        };

        compactLayout = new CompactLayout
        {
            OnRequestSessionListOpen = () => sessionListModal.Open()
        };
        portraitLayout = new PortraitLayout
        {
            OnRequestSessionListOpen = () => sessionListModal.Open()
        };
    }

    public void Dispose() => GC.SuppressFinalize(this);

    public override void Draw()
    {
        var availWidth = ImGui.GetContentRegionAvail().X;
        sessionListModal.Draw();

        // Get the current state from the media source and update the thumbnail texture if it has changed
        var state = mediaSource.CurrentState;
        mediaSource.TryGetThumbnail(out var thumbnailBytes);
        textures.UpdateIfChanged(BlobKeys.Thumbnail, state.HasThumbnail && thumbnailBytes.Length > 0 ? thumbnailBytes : null);

        // For SMTC, check if bridge is active
        if (!mediaSource.IsConnected && mediaSource.SourceName == "Windows SMTC")
        {
            var disconnectedText = "Not connected to ProxyPlayer server.";
            var textWidth = ImGui.CalcTextSize(disconnectedText).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - textWidth) / 2));
            ImGui.TextColored(ImGuiColors.DalamudRed, disconnectedText);
            return;
        }

        if (state.SelectedAppId == null)
        {
            var textWidth = ImGui.CalcTextSize(NoSessionSelectedText).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - (textWidth - 1)) / 2));
            ImGui.Text(NoSessionSelectedText);

            var sourceButtonText = "Select Music Source";
            var sourceButtonTextWidth = ImGui.CalcTextSize(sourceButtonText).X + 20; // Add some padding for the button
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - sourceButtonTextWidth) / 2));
            if (ImGui.Button($"{sourceButtonText}###MainWindowSelectSession"))
                sessionListModal.Open();
            return;
        }
        else
        {
            switch (plugin.Configuration.PlayerDisplayLayout)
            {
                case DisplayLayout.Compact:
                    compactLayout.Draw(state, mediaSource, textures);
                    break;
                case DisplayLayout.Portrait:
                    portraitLayout.Draw(state, mediaSource, textures);
                    break;
                default:
                    ImGui.Text("Unknown display layout.");
                    break;
            }
        }
    }
}
