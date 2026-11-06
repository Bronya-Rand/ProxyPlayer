using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ProxyPlayer.Media;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;
using ProxyPlayer.Utility;

namespace ProxyPlayer.Windows.Legacy.Layout
{
    /// <summary>
    /// Renders the player in a compact layout
    /// </summary>
    public sealed class CompactLayout : LayoutBase
    {
        private const string MetadataWidthPlaceholder = "MMMMMMMMMMMMMMMMMMMMMMMM";

        public override void Draw(MediaState state, IMediaSource mediaSource, TextureCache textures)
        {
            var available = ImGui.GetContentRegionAvail();
            var spacing = ImGui.GetStyle().ItemSpacing;

            var transportHeight = MeasureTransportHeight(state, available.X);
            var minimumMetadataWidth = ImGui.CalcTextSize(MetadataWidthPlaceholder).X;
            var maxArtHeight = available.Y - transportHeight - spacing.Y;
            var maxArtWidth = available.X - minimumMetadataWidth - spacing.X;

            var artSide = Math.Max(0f, Math.Min(maxArtHeight, maxArtWidth));

            var coverTexture = state.HasThumbnail ? textures.GetTexture(BlobKeys.Thumbnail) : null;
            if (coverTexture != null && artSide > 0f)
            {
                ImGui.Image(coverTexture.Handle, new Vector2(artSide, artSide));
                ImGui.SameLine();
            }

            using (ImRaii.Group())
            {
                var metadataLeft = ImGui.GetCursorPosX();
                var metadataWidth = available.X - spacing.X - artSide;

                var iconWidth = GetSessionSelectButtonSize().X;
                var sourceWidth = metadataWidth - spacing.X - iconWidth;
                var sourceFitsBesideButton = sourceWidth > 0;
                var sourceTextWidth = sourceFitsBesideButton ? sourceWidth : metadataWidth;

                // App Name (or address)
                // Use metadata width if sourceWidth is zero or negative
                var friendlyName = TrackMetadata.GetFriendlyAppName(state);
                ImPlayer.TextScrolling("session###CompactLayoutSessionName", friendlyName, sourceTextWidth,
                                       new Vector4(0.7f, 0.7f, 0.7f, 1f));

                if (sourceFitsBesideButton)
                {
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(metadataLeft + metadataWidth - iconWidth);
                }

                DrawSessionSelectButton("CompactLayoutSelectSession");

                ImPlayer.TextScrolling("title###CompactLayoutTitle", state.Title, metadataWidth);
                ImPlayer.TextScrolling("artist###CompactLayoutArtist", state.Artist, metadataWidth,
                                       new Vector4(0.7f, 0.7f, 0.7f, 1f));

                if (!state.Album.IsNullOrEmpty())
                    ImPlayer.TextScrolling("album###CompactLayoutAlbum", state.Album, metadataWidth,
                                           new Vector4(0.7f, 0.7f, 0.7f, 1f));
            }

            // Progress bar / Playback controls
            DrawTransportSection(state, mediaSource);
        }
    }
}
