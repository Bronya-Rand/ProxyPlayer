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
    private const string NoProxyPlayerServerText = "Not connected to ProxyPlayer server.";
    private const string NoSessionSelectedText = "No media session selected.";

    private readonly Plugin plugin;
    private readonly PipeClient pipeClient;
    private readonly TextureCache textures;
    private readonly SessionListModal sessionListModal;

    private readonly CompactLayout compactLayout;
    private readonly PortraitLayout portraitLayout;

    public MainWindow(Plugin plugin, PipeClient pipeClient)
        : base("ProxyPlayer - Now Playing##ProxyPlayerNowPlaying", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 150),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        SizeCondition = ImGuiCond.FirstUseEver;

        this.pipeClient = pipeClient;
        textures = new TextureCache();
        this.plugin = plugin;

        sessionListModal = new SessionListModal(pipeClient)
        {
            OnSelectSessionId = sessionId =>
                _ = pipeClient.SendCommandAsync(MediaCommand.SelectSession, sessionId)
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

        // Get the current state from the pipe client and update the thumbnail texture if it has changed
        var state = pipeClient.CurrentState;
        pipeClient.TryGetBlob(BlobKeys.Thumbnail, out var thumbnailBytes);
        textures.UpdateIfChanged(BlobKeys.Thumbnail, state.HasThumbnail && thumbnailBytes.Length > 0 ? thumbnailBytes : null);

        if (!pipeClient.IsConnected)
        {
            var textWidth = ImGui.CalcTextSize(NoProxyPlayerServerText).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - textWidth) / 2));
            ImGui.TextColored(ImGuiColors.DalamudRed, NoProxyPlayerServerText);
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
                    compactLayout.Draw(state, pipeClient, textures);
                    break;
                case DisplayLayout.Portrait:
                    portraitLayout.Draw(state, pipeClient, textures);
                    break;
                default:
                    ImGui.Text("Unknown display layout.");
                    break;
            }
        }
    }
}
