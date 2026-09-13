using System;
using System.Threading.Tasks;
using ProxyPlayer.Shared;

namespace ProxyPlayer.Models
{
    public interface IMediaSource : IDisposable
    {
        /// <summary>
        /// The name of the media source, used for logging and display purposes.
        /// </summary>
        string SourceName { get; }

        /// <summary>
        /// The current state of the media source, which may be null if the source is not connected or has not yet provided a state.
        /// </summary>
        MediaState CurrentState { get; }

        /// <summary>
        /// Indicates whether the media source is currently connected and able to provide state information.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Event that is triggered whenever the state of the media source is updated.
        /// </summary>
        event Action? OnStateUpdated;

        /// <summary>
        /// Attempts to retrieve a binary blob associated with the given key from the media source.
        /// </summary>
        /// <param name="key">The key of the blob to retrieve.</param>
        /// <param name="blob">The retrieved blob, or null if it could not be found.</param>
        /// <returns>true if the blob was found and retrieved; otherwise, false.</returns>
        bool TryGetBlob(string key, out byte[] blob);

        /// <summary>
        /// Sends a command to the media source, optionally targeting a specific application ID.
        /// </summary>
        /// <param name="command">The command to send.</param>
        /// <param name="targetAppId">The ID of the application to target, or null to target the default application.</param>
        /// <returns></returns>
        Task SendCommandAsync(MediaCommand command, string? targetAppId = null);

        bool TryGetThumbnail(out byte[] thumbnail) =>
            TryGetBlob(BlobKeys.Thumbnail, out thumbnail);
        Task PlayPauseAsync() => SendCommandAsync(MediaCommand.PlayPause);
        Task NextAsync() => SendCommandAsync(MediaCommand.Next);
        Task PreviousAsync() => SendCommandAsync(MediaCommand.Previous);
        Task StopAsync() => SendCommandAsync(MediaCommand.Stop);
        Task ToggleShuffleAsync() => SendCommandAsync(MediaCommand.ToggleShuffle);
        Task ToggleRepeatAsync() => SendCommandAsync(MediaCommand.ToggleRepeat);
        Task SelectSessionAsync(string appId) => SendCommandAsync(MediaCommand.SelectSession, appId);
    }
}
