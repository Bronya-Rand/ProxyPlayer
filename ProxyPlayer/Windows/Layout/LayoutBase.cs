using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ProxyPlayer.Media;
using ProxyPlayer.Models;
using ProxyPlayer.Utility;
using SamplePlugin.Shared;

namespace ProxyPlayer.Windows.Layout
{
    public abstract class LayoutBase : ILayoutBase
    {
        public virtual Vector2 CoverArtDimensions { get; }
        public Action? OnRequestSessionListOpen { get; set; }

        public abstract void Draw(MediaState mediaState, PipeClient pipeClient, TextureCache texture);

        /// <summary>
        /// Draws the progress bar for the given media state.
        /// </summary>
        /// <param name="mediaState">The media state to draw the progress bar for.</param>
        public static void DrawProgressBar(MediaState mediaState)
        {
            // Plugin.Log.Debug($"Current Position: {mediaState.PositionSeconds}, Duration: {mediaState.DurationSeconds}, Playback Status: {mediaState.PlaybackStatus}");
            if (mediaState.DurationSeconds > 0)
            {
                var currentPosition = TrackMetadata.GetInterpolatedPosition(mediaState);
                var progress = currentPosition / (float)mediaState.DurationSeconds;
                ImGui.ProgressBar(progress, new Vector2(-1, 2), string.Empty);

                using (ImRaii.Group())
                {
                    ImGui.Text($"{TimeSpan.FromSeconds(currentPosition):mm\\:ss}");
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 40);
                    ImGui.Text($"{TimeSpan.FromSeconds(mediaState.DurationSeconds):mm\\:ss}");
                }
            }
        }
        /// <summary>
        /// Draws the playback controls for the given media state, centered.
        /// </summary>
        /// <param name="mediaState">The media state to draw controls for.</param>
        /// <param name="pipeClient">The pipe client to use for sending commands.</param>
        public static void DrawPlaybackControlsCentered(MediaState mediaState, PipeClient pipeClient)
        {
            var availWidth = ImGui.GetContentRegionAvail().X;

            // Center the playback controls
            var spacing = ImGui.GetStyle().ItemSpacing.X;

            float prevWidth = 0, playWidth = 0, stopWidth = 0, nextWidth = 0, repeatWidth = 0, shuffleWidth = 0;
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                prevWidth = ImGui.CalcTextSize(FontAwesomeIcon.StepBackward.ToIconString()).X;
                playWidth = ImGui.CalcTextSize((mediaState.PlaybackStatus == "Playing" ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play).ToIconString()).X;
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
            if (mediaState.SupportsStop) { totalWidth += stopWidth; buttonCount++; }
            if (mediaState.SupportsRepeat) { totalWidth += repeatWidth; buttonCount++; }
            if (mediaState.SupportsShuffling) { totalWidth += shuffleWidth; buttonCount++; }
            totalWidth += spacing * (buttonCount - 1);

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availWidth - totalWidth) / 2));

            using (ImRaii.Group())
            {
                var first = true;

                if (mediaState.SupportsShuffling)
                {
                    var shuffleColor = mediaState.IsShuffleActive ? (Vector4?)new Vector4(0.2f, 0.8f, 0.2f, 1.0f) : null;
                    var shuffleTooltip = mediaState.IsShuffleActive ? "Shuffle: On" : "Shuffle: Off";
                    if (ImPlayer.DrawClickableIcon("shuffle", FontAwesomeIcon.Random, shuffleTooltip, shuffleColor))
                        _ = pipeClient.SendCommandAsync(MediaCommand.ToggleShuffle);
                    first = false;
                }

                if (mediaState.SupportsRepeat)
                {
                    var repeatActive = mediaState.RepeatMode == "List" || mediaState.RepeatMode == "Track";
                    var repeatColor = repeatActive ? (Vector4?)new Vector4(0.2f, 0.8f, 0.2f, 1.0f) : null;
                    var repeatTooltip = mediaState.RepeatMode switch
                    {
                        "Track" => "Repeat: Track",
                        "List" => "Repeat: List",
                        _ => "Repeat: Off"
                    };
                    if (!first) ImGui.SameLine();
                    if (ImPlayer.DrawClickableIcon("repeat", FontAwesomeIcon.Repeat, repeatTooltip, repeatColor))
                        _ = pipeClient.SendCommandAsync(MediaCommand.ToggleRepeat);
                    first = false;
                }

                if (!first) ImGui.SameLine();
                if (ImPlayer.DrawClickableIcon("previous", FontAwesomeIcon.StepBackward, "Previous"))
                    _ = pipeClient.SendCommandAsync(MediaCommand.Previous);
                first = false;

                if (!first) ImGui.SameLine();
                if (ImPlayer.DrawClickableIcon("play_pause", mediaState.PlaybackStatus == "Playing" ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play, "Play/Pause"))
                    _ = pipeClient.SendCommandAsync(MediaCommand.PlayPause);
                first = false;

                if (mediaState.SupportsStop)
                {
                    if (!first) ImGui.SameLine();
                    if (ImPlayer.DrawClickableIcon("stop", FontAwesomeIcon.Stop, "Stop"))
                        _ = pipeClient.SendCommandAsync(MediaCommand.Stop);
                    first = false;
                }

                if (!first) ImGui.SameLine();
                if (ImPlayer.DrawClickableIcon("next", FontAwesomeIcon.StepForward, "Next"))
                    _ = pipeClient.SendCommandAsync(MediaCommand.Next);
            }
        }
        public void DrawSessionSelectButton(string id)
        {
            if (ImPlayer.DrawClickableIcon($"select_session###{id}", FontAwesomeIcon.Sync, "Select Session"))
                OnRequestSessionListOpen?.Invoke();
        }
        public static string GetFriendlyAppName(MediaState mediaState)
        {
            if (mediaState.SelectedAppId != null && mediaState.AppFriendlyNames.TryGetValue(mediaState.SelectedAppId, out var name))
                return name;
            return mediaState.SelectedAppId ?? string.Empty;
        }
    }
}
