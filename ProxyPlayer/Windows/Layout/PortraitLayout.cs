using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ProxyPlayer.Media;
using SamplePlugin.Shared;

namespace ProxyPlayer.Windows.Layout
{
    /// <summary>
    /// Renders the player in a portrait layout
    /// </summary>
    public sealed class PortraitLayout : LayoutBase
    {
        public override Vector2 CoverArtDimensions => new(180, 180);

        public override void Draw(MediaState mediaState, PipeClient pipeClient, TextureCache texture)
        {
            var availWidth = ImGui.GetContentRegionAvail().X;

            // Centered App Name & Session Sync
            using (ImRaii.Group())
            {
                var friendlyName = GetFriendlyAppName(mediaState);

                var nameWidth = ImGui.CalcTextSize(friendlyName).X;
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - nameWidth) / 2));
                // App Name (or address)
                ImGui.TextDisabled(friendlyName);

                ImGui.SameLine();
                DrawSessionSelectButton("PortraitLayoutSelectSession");
            }

            ImGui.Spacing();

            // Centered Large Cover Art
            var coverTexture = mediaState.HasThumbnail ? texture.GetTexture(BlobKeys.Thumbnail) : null;
            if (coverTexture is { } tex)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - CoverArtDimensions.X) / 2));
                ImGui.Image(tex.Handle, CoverArtDimensions);
            }

            ImGui.Spacing();

            // Centered Media Info
            using (ImRaii.Group())
            {
                var titleWidth = ImGui.CalcTextSize(mediaState.Title).X;
                var artistWidth = ImGui.CalcTextSize(mediaState.Artist).X;

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - titleWidth) / 2));
                ImGui.Text(mediaState.Title);

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - artistWidth) / 2));
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), mediaState.Artist);

                if (!mediaState.Album.IsNullOrEmpty())
                {
                    var albumWidth = ImGui.CalcTextSize(mediaState.Album).X;
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - albumWidth) / 2));
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), mediaState.Album);
                }

                ImGui.Spacing();

                // Progress Bar
                DrawProgressBar(mediaState);

                // Playback Controls
                DrawPlaybackControlsCentered(mediaState, pipeClient);
            }
        }
    }
}
