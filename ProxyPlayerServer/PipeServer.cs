using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SamplePlugin.Shared;

namespace ProxyPlayerServer
{
    public sealed class PipeServer
    {
        private const string StatePipeName = "ProxyPlayerStatePipe";
        private const string CommandPipeName = "ProxyPlayerCommandPipe";
        private const int TaskDelayMilliseconds = 250;

        private readonly MediaSessionService media;
        private readonly CancellationTokenSource cts = new();
        private readonly SemaphoreSlim statePipeWriteLock = new(1, 1);

        private NamedPipeServerStream? currentStatePipe;

        public PipeServer(MediaSessionService media)
        {
            this.media = media;
            media.OnSessionChanged += () => _ = BroadcastStateAsync();
        }
        public void Start()
        {
            _ = Task.Run(() => RunStatePipeServerAsync(cts.Token));
            _ = Task.Run(() => RunCommandServerLoopAsync(cts.Token));
        }
        public void Stop() => cts.Cancel();

        /// <summary>
        /// Runs the state pipe server to broadcast the current media state to connected clients.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task RunStatePipeServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var pipeServer = NamedPipeServerStreamAcl.Create(
                    StatePipeName,
                    PipeDirection.Out,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    PipeSecurityHelper.CreateCurrentUserSecurity());

                try
                {
                    await pipeServer.WaitForConnectionAsync(cancellationToken);
                    currentStatePipe = pipeServer;

                    await WriteStatePipeAsync(pipeServer, BuildState(), cancellationToken);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (!pipeServer.IsConnected) break;

                        try
                        {
                            // Check if the pipe is not dead
                            await statePipeWriteLock.WaitAsync(cancellationToken);
                            try
                            {
                                await pipeServer.WriteAsync(Array.Empty<byte>(), cancellationToken);
                            }
                            finally
                            {
                                statePipeWriteLock.Release();
                            }
                        }
                        catch (IOException)
                        {
                            // The pipe is dead, break the loop to wait for a new connection
                            break;
                        }
                        await Task.Delay(TaskDelayMilliseconds, cancellationToken);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { /* client disconnected */ }
                finally
                {
                    currentStatePipe = null;
                }
            }
        }

        /// <summary>
        /// Broadcasts the current media state to the connected state pipe client, if any.
        /// </summary>
        /// <returns></returns>
        private async Task BroadcastStateAsync()
        {
            var pipe = currentStatePipe;
            if (pipe != null && pipe.IsConnected)
            {
                try { await WriteStatePipeAsync(pipe, BuildState(), cts.Token); }
                catch (IOException) { /* client disconnected */ }
            }
        }

        private async Task WriteStatePipeAsync(NamedPipeServerStream pipe, MessageEnvelope<MediaState> envelope, CancellationToken token)
        {
            await statePipeWriteLock.WaitAsync(token);
            try
            {
                await WriteEnvelopeAsync(pipe, envelope, token);
            }
            finally
            {
                statePipeWriteLock.Release();
            }
        }

        /// <summary>
        /// Listens for incoming command messages and executes the corresponding media control actions.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task RunCommandServerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                using var pipeServer = NamedPipeServerStreamAcl.Create(
                    CommandPipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    PipeSecurityHelper.CreateCurrentUserSecurity());

                try
                {
                    await pipeServer.WaitForConnectionAsync(token);

                    while (pipeServer.IsConnected && !token.IsCancellationRequested)
                    {
                        var message = await ReadMessageAsync<MediaCommandMessage>(pipeServer, token);
                        if (message == null) break;

                        switch (message.Command)
                        {
                            case MediaCommand.PlayPause: await media.PlayPauseSessionAsync(); break;
                            case MediaCommand.Stop: await media.StopAsync(); break;
                            case MediaCommand.Next: await media.NextAsync(); break;
                            case MediaCommand.Previous: await media.PreviousAsync(); break;
                            case MediaCommand.ToggleShuffle: await media.ToggleShuffleAsync(); break;
                            case MediaCommand.ToggleRepeat: await media.CycleRepeatModeAsync(); break;
                            case MediaCommand.SelectSession:
                                if (message.TargetAppId != null)
                                    media.SetSelectedSession(message.TargetAppId);
                                break;
                            default:
                                throw new InvalidOperationException("Unknown command: " + message.Command);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { /* client disconnected */ }
            }
        }

        /// <summary>
        /// Builds the current media state from the MediaSessionService.
        /// </summary>
        /// <returns></returns>
        private MessageEnvelope<MediaState> BuildState()
        {
            var sessions = media.GetAvailableSessions();
            var blobs = new Dictionary<string, byte[]>();

            if (media.CoverArtBytes is { Length: > 0 } coverArtBytes)
                blobs[BlobKeys.Thumbnail] = coverArtBytes;

            var state = new MediaState
            {
                Title = media.Title,
                Artist = media.Artist,
                Album = media.Album,
                PlaybackStatus = media.PlaybackStatus.ToString(),
                PositionSeconds = media.PositionSeconds,
                DurationSeconds = media.DurationSeconds,
                PositionLastUpdatedUtc = media.PositionLastUpdatedUtc,
                SelectedAppId = media.SelectedSessionId,
                AvailableAppIds = sessions,
                AppFriendlyNames = sessions.ToDictionary(id => id, id => Utils.ResolveFriendlyName(id)),
                SupportsRepeat = media.SupportsRepeat,
                SupportsShuffling = media.SupportsShuffle,
                SupportsStop = media.SupportsStop,
                IsShuffleActive = media.IsShuffleActive ?? false,
                RepeatMode = media.AutoRepeatMode?.ToString() ?? "None",
                HasThumbnail = blobs.ContainsKey(BlobKeys.Thumbnail)
            };

            return new MessageEnvelope<MediaState> { Payload = state, Blobs = blobs };
        }

        // Framing: (JSON-only messages): 4-byte prefix + JSON
        private static async Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken token)
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            var lengthPrefix = BitConverter.GetBytes(bytes.Length);
            await stream.WriteAsync(lengthPrefix, token);
            await stream.WriteAsync(bytes, token);
            await stream.FlushAsync(token);
        }
        private static async Task<T?> ReadMessageAsync<T>(Stream stream, CancellationToken token)
        {
            var lengthBuffer = new byte[4];
            if (!await ReadExactAsync(stream, lengthBuffer, token)) return default;

            var length = BitConverter.ToInt32(lengthBuffer);
            if (length <= 0 || length > 10_000_000) return default; // sanity check

            var buffer = new byte[length];
            if (!await ReadExactAsync(stream, buffer, token)) return default;
            return JsonSerializer.Deserialize<T>(buffer);
        }

        // Framing: (Envelope messages for state): 4B json length + json + 4B blob
        // count + repeated: [4B blob key length] + [blob key utf8] + [4B blob length] + [blob bytes]
        private static async Task WriteEnvelopeAsync<T>(Stream stream, MessageEnvelope<T> envelope, CancellationToken token)
        {
            var json = JsonSerializer.Serialize(envelope.Payload);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            await stream.WriteAsync(BitConverter.GetBytes(jsonBytes.Length), token);
            await stream.WriteAsync(jsonBytes, token);

            await stream.WriteAsync(BitConverter.GetBytes(envelope.Blobs.Count), token);
            foreach (var kvp in envelope.Blobs)
            {
                var keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                await stream.WriteAsync(BitConverter.GetBytes(keyBytes.Length), token);
                await stream.WriteAsync(keyBytes, token);
                await stream.WriteAsync(BitConverter.GetBytes(kvp.Value.Length), token);
                await stream.WriteAsync(kvp.Value, token);
            }

            await stream.FlushAsync(token);
        }
        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(offset), token);
                if (bytesRead == 0) return false; // disconnected
                offset += bytesRead;
            }
            return true;
        }
    }
}
