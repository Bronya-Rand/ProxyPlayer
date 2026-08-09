using System;
using SamplePlugin.Shared;

namespace ProxyPlayer.Utility
{
    public static class TrackMetadata
    {
        /// <summary>
        /// Returns a interpolated position based on the last known position and 
        /// the elapsed time since the last update.
        /// </summary>
        /// <remarks>
        /// Apps using SMTC do not send position updates periodically. Windows 
        /// interpolates the position based on the elapsed
        /// time since the last update.
        /// </remarks>
        /// <param name="state"></param>
        /// <returns></returns>
        public static float GetInterpolatedPosition(MediaState state)
        {
            if (state.PlaybackStatus != "Playing")
                return (float)state.PositionSeconds;

            // Calculate the elapsed time since the last position update
            var elapsedSeconds = (float)(DateTimeOffset.UtcNow - state.PositionLastUpdatedUtc).TotalSeconds;

            // Extrapolate the current position to track duration
            var calculatedPosition = state.PositionSeconds + elapsedSeconds;

            if (state.DurationSeconds > 0 && calculatedPosition > state.DurationSeconds)
                return (float)state.DurationSeconds;

            var fallback = Math.Max(0f, calculatedPosition);
            return (float)fallback;
        }
    }
}
