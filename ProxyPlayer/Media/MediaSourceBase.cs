using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;

namespace ProxyPlayer.Media
{
    public abstract class MediaSourceBase : IMediaSource
    {
        public abstract string SourceName { get; }

        public MediaState CurrentState { get; protected set; } = new();
        public bool IsConnected { get; protected set; }
        public event Action? OnStateUpdated;

        protected readonly CancellationTokenSource cts = new();
        protected Task? ReceiveLoopTask;

        /// <summary>
        /// Binary payload storage (e.g., cover art)
        /// </summary>
        protected Dictionary<string, byte[]> CurrentBlobs { get; set; } = [];

        public bool TryGetBlob(string key, out byte[] blob)
        {
            if (CurrentBlobs.TryGetValue(key, out var result))
            {
                blob = result;
                return true;
            }
            blob = [];
            return false;
        }

        public abstract Task SendCommandAsync(MediaCommand command, string? targetAppId = null);

        /// <summary>
        /// Notifies subscribers that the state of the media source has been updated.
        /// </summary>
        protected void NotifyStateUpdated() => OnStateUpdated?.Invoke();

        /// <summary>
        /// Resets the state of the media source when disconnected or no media session is active.
        /// </summary>
        protected virtual void ResetState()
        {
            CurrentState = new MediaState();
            CurrentBlobs.Clear();
            NotifyStateUpdated();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            cts.Cancel();
            try
            {
                ReceiveLoopTask?.GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Ignore exceptions during disposal
            }
            cts.Dispose();
        }
    }
}
