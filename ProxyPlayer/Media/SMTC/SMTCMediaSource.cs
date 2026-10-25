using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProxyPlayer.Shared;

namespace ProxyPlayer.Media.SMTC
{
    /// <summary>
    /// The client-side implementation of the named pipe communication with the ProxyPlayer process.
    /// </summary>
    public sealed class SmtcMediaSource : MediaSourceBase
    {
        public override string SourceName => "Windows SMTC";

        private const string StatePipeName = "ProxyPlayerStatePipe";
        private const string CommandPipeName = "ProxyPlayerCommandPipe";
        private const int ProxyReconnectTimeout = 2000;
        private const int CommandPipeTimeout = 1000;

        private readonly SemaphoreSlim commandLock = new(1, 1);
        private NamedPipeClientStream? commandPipe;

        public SmtcMediaSource() =>
            ReceiveLoopTask = Task.Run(() => StateReceiveLoopAsync(Cts.Token));

        private async Task StateReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var pipeClient =
                        new NamedPipeClientStream(".", StatePipeName, PipeDirection.In, PipeOptions.Asynchronous);
                    await pipeClient.ConnectAsync(cancellationToken)
                                    .ConfigureAwait(false); // Wait for the server to connect
                    IsConnected = true;
                    Plugin.Log.Debug("Connected to SMTC state pipe");

                    while (pipeClient.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var envelope = await ReadEnvelopeAsync<MediaState>(pipeClient, cancellationToken);
                        if (envelope == null) break;
                        CurrentState = envelope.Payload;
                        CurrentBlobs = envelope.Blobs;
                        NotifyStateUpdated();
                    }
                }
                catch (OperationCanceledException) { }
                catch (TimeoutException)
                {
                    Plugin.Log.Debug("State pipe timeout");
                }
                catch (IOException)
                {
                    Plugin.Log.Debug("State pipe disconnected");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warning(ex, "Unexpected error in state receive loop");
                } finally
                {
                    IsConnected = false;

                    // Guard against semaphore being disposed by Dispose() concurrently
                    try
                    {
                        commandLock.Wait(CancellationToken.None);
                        try
                        {
                            commandPipe?.Dispose();
                            commandPipe = null;
                        } finally
                        {
                            commandLock.Release();
                        }
                    }
                    catch (ObjectDisposedException) { }
                }

                if (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(ProxyReconnectTimeout, cancellationToken); // Wait before trying to reconnect
            }
        }

        public override async Task SendCommandAsync(MediaCommand command, string? targetAppId = null)
        {
            await commandLock.WaitAsync(Cts.Token).ConfigureAwait(false);
            try
            {
                if (commandPipe is not { IsConnected: true })
                {
                    commandPipe?.Dispose();
                    commandPipe =
                        new NamedPipeClientStream(".", CommandPipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                    await commandPipe.ConnectAsync(CommandPipeTimeout); // Wait for the server to connect
                }

                await WriteMessageAsync(commandPipe,
                                        new MediaCommandMessage { Command = command, TargetAppId = targetAppId },
                                        Cts.Token);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Failed to send command to proxy");
                commandPipe?.Dispose();
                commandPipe = null;
            } finally
            {
                commandLock.Release();
            }
        }

        private static async Task WriteMessageAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(value, typeof(T), ProxyPlayerJsonContext.Default);
            var bytes = Encoding.UTF8.GetBytes(json);
            await stream.WriteAsync(BitConverter.GetBytes(bytes.Length), cancellationToken);
            await stream.WriteAsync(bytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static async Task<MessageEnvelope<T>?> ReadEnvelopeAsync<T>(
            Stream stream, CancellationToken cancellationToken)
        {
            var jsonLengthBuffer = new byte[4];
            if (!await ReadExactAsync(stream, jsonLengthBuffer, cancellationToken)) return null;
            var jsonLength = BitConverter.ToInt32(jsonLengthBuffer, 0);
            if (jsonLength is <= 0 or > 10_000_000) return null; // sanity check

            var jsonBuffer = new byte[jsonLength];
            if (!await ReadExactAsync(stream, jsonBuffer, cancellationToken)) return null;
            var payload = (T?)JsonSerializer.Deserialize(jsonBuffer, typeof(T), ProxyPlayerJsonContext.Default);
            if (payload == null) return null;

            var blobCountBuffer = new byte[4];
            if (!await ReadExactAsync(stream, blobCountBuffer, cancellationToken)) return null;
            var blobCount = BitConverter.ToInt32(blobCountBuffer, 0);
            if (blobCount is < 0 or > 10_000_000) return null; // sanity check

            var blobs = new Dictionary<string, byte[]>();
            for (var i = 0; i < blobCount; i++)
            {
                var keyLengthBuffer = new byte[4];
                if (!await ReadExactAsync(stream, keyLengthBuffer, cancellationToken)) return null;
                var keyLength = BitConverter.ToInt32(keyLengthBuffer, 0);
                if (keyLength is <= 0 or > 10_000_000) return null; // sanity check

                var keyBuffer = new byte[keyLength];
                if (!await ReadExactAsync(stream, keyBuffer, cancellationToken)) return null;
                var key = Encoding.UTF8.GetString(keyBuffer);

                var dataLengthBuffer = new byte[4];
                if (!await ReadExactAsync(stream, dataLengthBuffer, cancellationToken)) return null;
                var dataLength = BitConverter.ToInt32(dataLengthBuffer, 0);
                if (dataLength is < 0 or > 10_000_000) return null; // sanity check

                var dataBuffer = new byte[dataLength];
                if (!await ReadExactAsync(stream, dataBuffer, cancellationToken)) return null;

                blobs[key] = dataBuffer;
            }

            return new MessageEnvelope<T> { Payload = payload, Blobs = blobs };
        }

        private static async Task<bool> ReadExactAsync(
            Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var bytesRead =
                    await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
                if (bytesRead == 0) return false; // End of stream
                offset += bytesRead;
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                commandPipe?.Dispose();
                commandLock.Dispose();
            }
        }
    }
}
