using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ProxyPlayer.Media;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;
using ProxyPlayer.Utility;

namespace ProxyPlayer.Windows.Layout
{
    /// <summary>
    /// Renders the player in a portrait layout
    /// </summary>
    public sealed class PortraitLayout : LayoutBase
    {
        public override void Draw(MediaState state, IMediaSource mediaSource, TextureCache texture)
        {
            var contentLeft = ImGui.GetCursorPosX();
            var availWidth = ImGui.GetContentRegionAvail().X;
            var spacing = ImGui.GetStyle().ItemSpacing.X;

            // Centered App Name & Session Sync
            using (ImRaii.Group())
            {
                var iconWidth = GetSessionSelectButtonSize().X;
                var sourceWidth = availWidth - spacing - iconWidth;
                var friendlyName = TrackMetadata.GetFriendlyAppName(state);
                var nameWidth = ImGui.CalcTextSize(friendlyName).X;

                if (availWidth > 0)
                {
                    ImGui.SetCursorPosX(contentLeft);

                    if (nameWidth <= sourceWidth)
                    {
                        // Center Name and Icon
                        var combinedWidth = nameWidth + spacing + iconWidth;
                        ImGui.SetCursorPosX(contentLeft + ((availWidth - combinedWidth) / 2));

                        ImGui.TextDisabled(friendlyName);
                        ImGui.SameLine();
                    }
                    else if (sourceWidth > 0)
                    {
                        // Text Scroll / Right-Align Icon
                        ImPlayer.TextScrolling("session###PortraitLayoutSessionName", friendlyName, sourceWidth,
                                               new Vector4(0.7f, 0.7f, 0.7f, 1f));
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(contentLeft + availWidth - iconWidth);
                    }
                    else
                    {
                        // Separate Rows
                        ImPlayer.TextScrolling("session###PortraitLayoutSessionName", friendlyName, availWidth,
                                               new Vector4(0.7f, 0.7f, 0.7f, 1f));

                        ImGui.SetCursorPosX(contentLeft + Math.Max(0f, (availWidth - iconWidth) / 2));
                    }

                    DrawSessionSelectButton("PortraitLayoutSelectSession");
                }
            }

            ImGui.Spacing();

            // Centered Large Cover Art
            var available = ImGui.GetContentRegionAvail();
            var reservedBelowHeight = MeasureMetadataHeight(state)
                                      + MeasureTransportHeight(state, availWidth)
                                      + ImGui.GetStyle().ItemSpacing.Y;

            var artSide = Math.Max(
                0f,
                Math.Min(available.X, available.Y - reservedBelowHeight - ImGui.GetStyle().ItemSpacing.Y));
            var coverTexture = state.HasThumbnail ? texture.GetTexture(BlobKeys.Thumbnail) : null;
            if (coverTexture != null && artSide > 0)
            {
                ImGui.SetCursorPosX(contentLeft + ((availWidth - artSide) / 2));
                ImGui.Image(coverTexture.Handle, new Vector2(artSide, artSide));
            }

            ImGui.Spacing();

            // Centered Media Info
            ImGui.SetCursorPosX(contentLeft);
            using (ImRaii.Group())
            {
                var titleWidth = ImGui.CalcTextSize(state.Title).X;
                var artistWidth = ImGui.CalcTextSize(state.Artist).X;

                if (titleWidth <= availWidth)
                {
                    ImGui.SetCursorPosX(contentLeft + (availWidth - titleWidth) / 2);
                    ImGui.Text(state.Title);
                }
                else if (availWidth > 0)
                {
                    ImGui.SetCursorPosX(contentLeft);
                    ImPlayer.TextScrolling("title###PortraitLayoutTitle", state.Title, availWidth);
                }

                if (artistWidth <= availWidth)
                {
                    ImGui.SetCursorPosX(contentLeft + (availWidth - artistWidth) / 2);
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), state.Artist);
                }
                else if (availWidth > 0)
                {
                    ImGui.SetCursorPosX(contentLeft);
                    ImPlayer.TextScrolling("artist###PortraitLayoutArtist", state.Artist, availWidth,
                                           new Vector4(0.7f, 0.7f, 0.7f, 1f));
                }

                if (!state.Album.IsNullOrEmpty())
                {
                    var albumWidth = ImGui.CalcTextSize(state.Album).X;
                    if (albumWidth <= availWidth)
                    {
                        ImGui.SetCursorPosX(contentLeft + (availWidth - albumWidth) / 2);
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), state.Album);
                    }
                    else if (availWidth > 0)
                    {
                        ImGui.SetCursorPosX(contentLeft);
                        ImPlayer.TextScrolling("album###PortraitLayoutAlbum", state.Album, availWidth,
                                               new Vector4(0.7f, 0.7f, 0.7f, 1f));
                    }
                }
            }

            // Progress bar / Playback controls
            ImGui.SetCursorPosX(contentLeft);
            DrawTransportSection(state, mediaSource);
        }

        private static float MeasureMetadataHeight(MediaState state)
        {
            var lineHeight = ImGui.GetTextLineHeight();

            var titleHeight = Math.Max(lineHeight, ImGui.CalcTextSize(state.Title).Y);
            var artistHeight = Math.Max(lineHeight, ImGui.CalcTextSize(state.Artist).Y);

            var hasAlbum = !state.Album.IsNullOrEmpty();
            var albumHeight = hasAlbum
                                  ? Math.Max(lineHeight, ImGui.CalcTextSize(state.Album).Y)
                                  : 0f;

            var spacing = ImGui.GetStyle().ItemSpacing.Y * (hasAlbum ? 3 : 2);

            return titleHeight + artistHeight + albumHeight + spacing;
        }
    }
}
