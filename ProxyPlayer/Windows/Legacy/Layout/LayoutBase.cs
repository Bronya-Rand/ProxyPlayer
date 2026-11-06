using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ProxyPlayer.Media;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;
using ProxyPlayer.Utility;

namespace ProxyPlayer.Windows.Legacy.Layout
{
    public abstract class LayoutBase : ILayoutBase
    {
        private const FontAwesomeIcon SyncIcon = FontAwesomeIcon.Sync;
        private const float ProgressBarHeight = 2f;
        public Action? OnRequestSessionListOpen { get; set; }

        public abstract void Draw(MediaState mediaState, IMediaSource mediaSource, TextureCache texture);

        /// <summary>
        /// Draws the progress bar for the given media state.
        /// </summary>
        /// <param name="mediaState">The media state to draw the progress bar for.</param>
        /// <param name="width">The available width space in the media window.</param>
        private static void DrawProgressBar(MediaState mediaState, float width)
        {
            if (mediaState.DurationSeconds > 0)
            {
                var currentPosition = TrackMetadata.GetInterpolatedPosition(mediaState);
                var progress = currentPosition / (float)mediaState.DurationSeconds;
                ImGui.ProgressBar(progress, new Vector2(width, ProgressBarHeight), string.Empty);

                using (ImRaii.Group())
                {
                    var positionText = $"{TimeSpan.FromSeconds(currentPosition):mm\\:ss}";
                    var durationText = $"{TimeSpan.FromSeconds(mediaState.DurationSeconds):mm\\:ss}";

                    var rowLeft = ImGui.GetCursorPosX();
                    var durationWidth = ImGui.CalcTextSize(durationText).X;
                    var durationX = rowLeft + width - durationWidth;
                    var requiredWidth = ImGui.CalcTextSize(positionText).X
                                        + ImGui.GetStyle().ItemSpacing.X
                                        + durationWidth;

                    ImGui.Text(positionText);
                    if (requiredWidth <= width)
                    {
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(durationX);
                    }

                    ImGui.Text(durationText);
                }
            }
        }

        /// <summary>
        /// Draws the playback controls for the given media state, centered.
        /// </summary>
        /// <param name="mediaState">The media state to draw controls for.</param>
        /// <param name="mediaSource">The pipe client to use for sending commands.</param>
        /// <param name="width">The available width space in the media window.</param>
        private static void DrawPlaybackControlsCentered(MediaState mediaState, IMediaSource mediaSource, float width)
        {
            // Center the playback controls
            var spacing = ImGui.GetStyle().ItemSpacing.X;

            float prevWidth, playWidth, stopWidth = 0, nextWidth, repeatWidth = 0, shuffleWidth = 0;
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                prevWidth = ImGui.CalcTextSize(FontAwesomeIcon.StepBackward.ToIconString()).X;
                playWidth = ImGui
                            .CalcTextSize((mediaState.PlaybackStatus == "Playing"
                                               ? FontAwesomeIcon.Pause
                                               : FontAwesomeIcon.Play).ToIconString()).X;
                nextWidth = ImGui.CalcTextSize(FontAwesomeIcon.StepForward.ToIconString()).X;
                if (mediaState.SupportsStop)
                    stopWidth = ImGui.CalcTextSize(FontAwesomeIcon.Stop.ToIconString()).X;
                if (mediaState.SupportsRepeat)
                    repeatWidth = ImGui.CalcTextSize(FontAwesomeIcon.Repeat.ToIconString()).X;
                if (mediaState.SupportsShuffling)
                    shuffleWidth = ImGui.CalcTextSize(FontAwesomeIcon.Random.ToIconString()).X;
            }

            var totalWidth = prevWidth + playWidth + nextWidth;
            var buttonCount = 3;
            if (mediaState.SupportsStop)
            {
                totalWidth += stopWidth;
                buttonCount++;
            }

            if (mediaState.SupportsRepeat)
            {
                totalWidth += repeatWidth;
                buttonCount++;
            }

            if (mediaState.SupportsShuffling)
            {
                totalWidth += shuffleWidth;
                buttonCount++;
            }

            totalWidth += spacing * (buttonCount - 1);

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((width - totalWidth) / 2));

            using (ImRaii.Group())
            {
                var first = true;

                if (mediaState.SupportsShuffling)
                {
                    var shuffleColor = mediaState.IsShuffleActive
                                           ? (Vector4?)new Vector4(0.2f, 0.8f, 0.2f, 1.0f)
                                           : null;
                    var shuffleTooltip = mediaState.IsShuffleActive ? "Shuffle: On" : "Shuffle: Off";
                    if (ImPlayer.DrawClickableIcon("shuffle", FontAwesomeIcon.Random, shuffleTooltip, shuffleColor))
                        _ = mediaSource.ToggleShuffleAsync();
                    first = false;
                }

                if (mediaState.SupportsRepeat)
                {
                    var repeatActive = mediaState.RepeatMode == "List" || mediaState.RepeatMode == "Track";
                    var repeatColor = repeatActive ? (Vector4?)new Vector4(0.2f, 0.8f, 0.2f, 1.0f) : null;
                    var repeatTooltip = mediaState.RepeatMode switch
                    {
                        "Track" => "Repeat: Track",
                        "List" => "Repeat: All",
                        _ => "Repeat: Off"
                    };
                    if (!first) ImGui.SameLine();
                    if (ImPlayer.DrawClickableIcon("repeat", FontAwesomeIcon.Repeat, repeatTooltip, repeatColor))
                        _ = mediaSource.ToggleRepeatAsync();
                    first = false;
                }

                if (!first) ImGui.SameLine();
                if (ImPlayer.DrawClickableIcon("previous", FontAwesomeIcon.StepBackward, "Previous"))
                    _ = mediaSource.PreviousAsync();
                first = false;

                if (!first) ImGui.SameLine();
                if (ImPlayer.DrawClickableIcon("play_pause",
                                               mediaState.PlaybackStatus == "Playing"
                                                   ? FontAwesomeIcon.Pause
                                                   : FontAwesomeIcon.Play, "Play/Pause"))
                    _ = mediaSource.PlayPauseAsync();
                first = false;

                if (mediaState.SupportsStop)
                {
                    if (!first) ImGui.SameLine();
                    if (ImPlayer.DrawClickableIcon("stop", FontAwesomeIcon.Stop, "Stop"))
                        _ = mediaSource.StopAsync();
                    first = false;
                }

                if (!first) ImGui.SameLine();
                if (ImPlayer.DrawClickableIcon("next", FontAwesomeIcon.StepForward, "Next"))
                    _ = mediaSource.NextAsync();
            }
        }

        protected static Vector2 GetSessionSelectButtonSize()
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                return ImGui.CalcTextSize(SyncIcon.ToIconString());
            }
        }

        protected void DrawSessionSelectButton(string id)
        {
            if (ImPlayer.DrawClickableIcon($"select_session###{id}", FontAwesomeIcon.Sync, "Select Session"))
                OnRequestSessionListOpen?.Invoke();
        }

        protected static void DrawTransportSection(MediaState state, IMediaSource mediaSource)
        {
            var width = ImGui.GetContentRegionAvail().X;

            DrawProgressBar(state, width);
            DrawPlaybackControlsCentered(state, mediaSource, width);
        }

        protected static float MeasureTransportHeight(MediaState state, float width)
        {
            var spacing = ImGui.GetStyle().ItemSpacing;
            var controlsHeight = 0f;

            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                void IncludeIcon(FontAwesomeIcon icon)
                {
                    controlsHeight = Math.Max(controlsHeight,
                                              ImGui.CalcTextSize(icon.ToIconString()).Y);
                }

                IncludeIcon(FontAwesomeIcon.StepBackward);
                IncludeIcon(state.PlaybackStatus == "Playing" ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play);
                IncludeIcon(FontAwesomeIcon.StepForward);

                if (state.SupportsStop)
                    IncludeIcon(FontAwesomeIcon.Stop);
                if (state.SupportsRepeat)
                    IncludeIcon(FontAwesomeIcon.Repeat);
                if (state.SupportsShuffling)
                    IncludeIcon(FontAwesomeIcon.Random);
            }

            if (state.DurationSeconds <= 0f)
                return controlsHeight;

            var position = TrackMetadata.GetInterpolatedPosition(state);
            var positionText = $"{TimeSpan.FromSeconds(position):mm\\:ss}";
            var durationText = $"{TimeSpan.FromSeconds(state.DurationSeconds):mm\\:ss}";

            var positionSize = ImGui.CalcTextSize(positionText);
            var durationSize = ImGui.CalcTextSize(durationText);

            var timesFit = positionSize.X + spacing.X + durationSize.X <= width;

            var timesHeight = timesFit
                                  ? Math.Max(positionSize.Y, durationSize.Y)
                                  : positionSize.Y + spacing.Y + durationSize.Y;

            return ProgressBarHeight
                   + spacing.Y
                   + timesHeight
                   + spacing.Y
                   + controlsHeight;
        }
    }
}
