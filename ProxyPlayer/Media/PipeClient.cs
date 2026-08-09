using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProxyPlayer.Shared;

namespace ProxyPlayer.Media
{
    /// <summary>
    /// The client-side implementation of the named pipe communication with the ProxyPlayer process.
    /// </summary>
    public sealed class PipeClient : IDisposable
    {
        private const string StatePipeName = "ProxyPlayerStatePipe";
        private const string CommandPipeName = "ProxyPlayerCommandPipe";
        private const int ProxyReconnectTimeout = 2000;
        private const int CommandPipeTimeout = 1000;

        private readonly CancellationTokenSource cts = new();
        private readonly SemaphoreSlim commandLock = new(1, 1);
        private NamedPipeClientStream? commandPipe;

        public MediaState CurrentState { get; private set; } = new();
        public bool IsConnected { get; private set; }
        public event Action? OnStateUpdated;

        // Binary data that is sent alongside CurrentState
        private Dictionary<string, byte[]> currentBlobs = [];

        public bool TryGetBlob(string key, out byte[] blob)
        {
            if (currentBlobs.TryGetValue(key, out var result))
            {
                blob = result;
                return true;
            }
            blob = [];
            return false;
        }

        public PipeClient()
        {
            _ = Task.Run(() => StateReceiveLoopAsync(cts.Token));
        }

        private async Task StateReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var pipeClient = new NamedPipeClientStream(".", StatePipeName, PipeDirection.In, PipeOptions.Asynchronous);
                    await pipeClient.ConnectAsync(cancellationToken); // Wait for the server to connect
                    IsConnected = true;
                    Plugin.Log.Debug("State pipe connected");

                    while (pipeClient.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var envelope = await ReadEnvelopeAsync<MediaState>(pipeClient, cancellationToken);
                        if (envelope == null) break;
                        CurrentState = envelope.Payload;
                        currentBlobs = envelope.Blobs;
                        OnStateUpdated?.Invoke();
                    }
                }
                catch (OperationCanceledException) { }
                catch (TimeoutException) { Plugin.Log.Debug("State pipe timeout"); }
                catch (IOException) { Plugin.Log.Debug("State pipe disconnected"); }
                catch (Exception ex)
                {
                    Plugin.Log.Warning(ex, "Unexpected error in state receive loop");
                }
                finally
                {
                    IsConnected = false;
                    commandLock.Wait();
                    try
                    {
                        commandPipe?.Dispose();
                        commandPipe = null;
                    }
                    finally
                    {
                        commandLock.Release();
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(ProxyReconnectTimeout, cancellationToken); // Wait before trying to reconnect
            }
        }
        public async Task SendCommandAsync(MediaCommand command, string? targetAppId = null)
        {
            await commandLock.WaitAsync();
            try
            {
                if (commandPipe == null || !commandPipe.IsConnected)
                {
                    commandPipe?.Dispose();
                    commandPipe = new NamedPipeClientStream(".", CommandPipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                    await commandPipe.ConnectAsync(CommandPipeTimeout); // Wait for the server to connect
                }
                await WriteMessageAsync(commandPipe, new MediaCommandMessage { Command = command, TargetAppId = targetAppId }, cts.Token);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Failed to send command to proxy");
                commandPipe?.Dispose();
                commandPipe = null;
            }
            finally
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
        private static async Task<MessageEnvelope<T>?> ReadEnvelopeAsync<T>(Stream stream, CancellationToken cancellationToken)
        {
            var jsonLengthBuffer = new byte[4];
            if (!await ReadExactAsync(stream, jsonLengthBuffer, cancellationToken)) return default;
            var jsonLength = BitConverter.ToInt32(jsonLengthBuffer, 0);
            if (jsonLength <= 0 || jsonLength > 10_000_000) return null; // sanity check

            var jsonBuffer = new byte[jsonLength];
            if (!await ReadExactAsync(stream, jsonBuffer, cancellationToken)) return default;
            var payload = (T?)JsonSerializer.Deserialize(jsonBuffer, typeof(T), ProxyPlayerJsonContext.Default);
            if (payload == null) return null;

            var blobCountBuffer = new byte[4];
            if (!await ReadExactAsync(stream, blobCountBuffer, cancellationToken)) return default;
            var blobCount = BitConverter.ToInt32(blobCountBuffer, 0);
            if (blobCount < 0 || blobCount > 10_000_000) return null; // sanity check

            var blobs = new Dictionary<string, byte[]>();
            for (var i = 0; i < blobCount; i++)
            {
                var keyLengthBuffer = new byte[4];
                if (!await ReadExactAsync(stream, keyLengthBuffer, cancellationToken)) return default;
                var keyLength = BitConverter.ToInt32(keyLengthBuffer, 0);
                if (keyLength <= 0 || keyLength > 10_000_000) return null; // sanity check

                var keyBuffer = new byte[keyLength];
                if (!await ReadExactAsync(stream, keyBuffer, cancellationToken)) return default;
                var key = Encoding.UTF8.GetString(keyBuffer);

                var dataLengthBuffer = new byte[4];
                if (!await ReadExactAsync(stream, dataLengthBuffer, cancellationToken)) return default;
                var dataLength = BitConverter.ToInt32(dataLengthBuffer, 0);
                if (dataLength < 0 || dataLength > 10_000_000) return null; // sanity check

                var dataBuffer = new byte[dataLength];
                if (!await ReadExactAsync(stream, dataBuffer, cancellationToken)) return default;

                var data = dataBuffer;
                blobs[key] = data;
            }

            return new MessageEnvelope<T> { Payload = payload, Blobs = blobs };
        }
        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
                if (bytesRead == 0) return false; // End of stream
                offset += bytesRead;
            }
            return true;
        }
        public void Dispose()
        {
            cts.Cancel();
            commandPipe?.Dispose();
            cts.Dispose();
            commandLock.Dispose();
        }
    }
}

