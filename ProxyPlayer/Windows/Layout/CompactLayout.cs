using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ProxyPlayer.Media;
using ProxyPlayer.Shared;
using ProxyPlayer.Utility;

namespace ProxyPlayer.Windows.Layout
{
    /// <summary>
    /// Renders the player in a compact layout
    /// </summary>
    public sealed class CompactLayout : LayoutBase
    {
        public override void Draw(MediaState state, PipeClient pipeClient, TextureCache textures)
        {
            var lineCount = string.IsNullOrEmpty(state.Album) ? 3 : 4;
            var lineHeight = ImGui.GetTextLineHeight();
            var spacingY = ImGui.GetStyle().ItemSpacing.Y;
            var artHeight = (lineCount * lineHeight) + ((lineCount - 1) * spacingY);
            var artSize = new Vector2(artHeight, artHeight);

            var coverTexture = state.HasThumbnail ? textures.GetTexture(BlobKeys.Thumbnail) : null;
            if (coverTexture is { } tex)
            {
                ImGui.Image(tex.Handle, artSize);
                ImGui.SameLine();
            }

            using (ImRaii.Group())
            {
                var friendlyName = TrackMetadata.GetFriendlyAppName(state);

                // App Name (or address)
                ImGui.TextDisabled(friendlyName);

                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 24);
                DrawSessionSelectButton("CompactLayoutSelectSession");

                var metadataAvailWidth = ImGui.GetContentRegionAvail().X;
                ImPlayer.TextScrolling("title###CompactLayoutTitle", state.Title, metadataAvailWidth);
                ImPlayer.TextScrolling("artist###CompactLayoutArtist", state.Artist, metadataAvailWidth, new Vector4(0.7f, 0.7f, 0.7f, 1f));

                if (!state.Album.IsNullOrEmpty())
                    ImPlayer.TextScrolling("album###CompactLayoutAlbum", state.Album, metadataAvailWidth, new Vector4(0.7f, 0.7f, 0.7f, 1f));
            }

            // Progress bar
            DrawProgressBar(state);

            // Playback controls
            DrawPlaybackControlsCentered(state, pipeClient);
        }
    }
}
