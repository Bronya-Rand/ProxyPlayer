using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using Mpris.MediaPlayer2; // XML in Interpolation
using Mpris.MediaPlayer2.Player; // XML in Interpolation
using ProxyPlayer.Shared;
using Tmds.DBus.Protocol;

namespace ProxyPlayer.Media.MPRIS
{
    public sealed record NameOwnerChanged(string Name, string OldOwner, string NewOwner);
    public sealed class MprisMediaSource : MediaSourceBase
    {
        public override string SourceName => "Linux MPRIS";

        private const string MprisPrefix = "org.mpris.MediaPlayer2.";
        private const string PlayerPath = "/org/mpris/MediaPlayer2";
        private const int PositionPollMs = 500;
        private const float UsToSeconds = 1_000_000f;

        private readonly SemaphoreSlim sessionLock = new(1, 1);
        private readonly HttpClient httpClient = new();

        private DBusConnection? dBus;
        private IDisposable? nameOwnerWatch;
        private IDisposable? propertiesWatch;

        private readonly List<string> knownPlayers = [];
        private string? selectedPlayer;

        // Cached position for MPRIS
        private double lastKnownPositionSeconds;
        private DateTimeOffset lastKnownPositionUpdatedUtc = DateTimeOffset.UtcNow;

        public MprisMediaSource()
        {
            ReceiveLoopTask = Task.Run(() => ConnectLoopAsync(cts.Token));
            _ = Task.Run(() => PositionPollLoopAsync(cts.Token));
        }

        private async Task ConnectLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var address = DBusAddress.Session
                        ?? throw new InvalidOperationException("DBus session address not found.");

                    dBus = new DBusConnection(address);
                    await dBus.ConnectAsync().ConfigureAwait(false);
                    IsConnected = true;
                    Plugin.Log.Debug("Connected to DBus session bus");

                    nameOwnerWatch = await dBus.AddMatchAsync(
                        new MatchRule
                        {
                            Type = MessageType.Signal,
                            Sender = "org.freedesktop.DBus",
                            Path = "/org/freedesktop/DBus",
                            Interface = "org.freedesktop.DBus",
                            Member = "NameOwnerChanged",
                        },
                        ReadNameOwnerChanged,
                        HandleNameOwnerChangedAsync,
                        emitOnCapturedContext: false).ConfigureAwait(false);

                    await RefreshPlayerListAsync(cancellationToken).ConfigureAwait(false);

                    // Block until cancellation is requested
                    while (!cancellationToken.IsCancellationRequested)
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Plugin.Log.Warning(ex, "Unexpected error in DBus connect loop");
                }
                finally
                {
                    IsConnected = false;
                    propertiesWatch?.Dispose();
                    propertiesWatch = null;
                    nameOwnerWatch?.Dispose();
                    nameOwnerWatch = null;
                    dBus?.Dispose();
                    dBus = null;
                    ResetState();
                }

                if (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false); // Wait before trying to reconnect
            }
        }
        private async Task RefreshPlayerListAsync(CancellationToken cancellationToken)
        {
            if (dBus == null) return;

            var names = await dBus.ListServicesAsync().ConfigureAwait(false);
            var mprisPlayers = names.Where(name => name.StartsWith(MprisPrefix, StringComparison.OrdinalIgnoreCase));

            await sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                knownPlayers.Clear();
                knownPlayers.AddRange(mprisPlayers);

                if (selectedPlayer == null || !knownPlayers.Contains(selectedPlayer))
                    selectedPlayer = knownPlayers.FirstOrDefault();

                await SwitchActivePlayerAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private static NameOwnerChanged? ReadNameOwnerChanged(Message message, object? state)
        {
            var reader = message.GetBodyReader();
            var name = reader.ReadString();
            if (!name.StartsWith(MprisPrefix, StringComparison.Ordinal)) return null;

            var result = new NameOwnerChanged(name, reader.ReadString(), reader.ReadString());
            return result;
        }
        private async ValueTask HandleNameOwnerChangedAsync(Notification<NameOwnerChanged?> notification)
        {
            if (!notification.HasValue || notification.Value is not NameOwnerChanged { } nameOwnerChanged) return;

            await sessionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var appeared = nameOwnerChanged.OldOwner.IsNullOrEmpty() && !nameOwnerChanged.NewOwner.IsNullOrEmpty();
                var disappeared = !nameOwnerChanged.OldOwner.IsNullOrEmpty() && nameOwnerChanged.NewOwner.IsNullOrEmpty();

                if (appeared && !knownPlayers.Contains(nameOwnerChanged.Name))
                {
                    knownPlayers.Add(nameOwnerChanged.Name);
                    Plugin.Log.Debug($"MPRIS player appeared: {nameOwnerChanged.Name}");
                }
                else if (disappeared && knownPlayers.Contains(nameOwnerChanged.Name))
                {
                    knownPlayers.Remove(nameOwnerChanged.Name);
                    Plugin.Log.Debug($"MPRIS player disappeared: {nameOwnerChanged.Name}");
                }
                else
                    return;

                // Keep selection unless the selected player disappeared
                if (selectedPlayer == null || !knownPlayers.Contains(selectedPlayer))
                    selectedPlayer = knownPlayers.FirstOrDefault();

                await SwitchActivePlayerAsync(cts.Token).ConfigureAwait(false);
            }
            finally
            {
                sessionLock.Release();
            }
        }
        public override async Task SendCommandAsync(MediaCommand command, string? targetAppId = null)
        {
            if (dBus == null) return;

            if (command == MediaCommand.SelectSession && !targetAppId.IsNullOrEmpty())
            {
                await sessionLock.WaitAsync(cts.Token).ConfigureAwait(false);
                try
                {
                    selectedPlayer = targetAppId;
                    await SwitchActivePlayerAsync(cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    sessionLock.Release();
                }
                return;
            }

            if (selectedPlayer == null) return;
            var player = new Player(dBus, selectedPlayer, PlayerPath);

            switch (command)
            {
                case MediaCommand.PlayPause:
                    await player.PlayPauseAsync().ConfigureAwait(false);
                    break;
                case MediaCommand.Next:
                    await player.NextAsync().ConfigureAwait(false);
                    break;
                case MediaCommand.Previous:
                    await player.PreviousAsync().ConfigureAwait(false);
                    break;
                case MediaCommand.Stop:
                    await player.StopAsync().ConfigureAwait(false);
                    break;
                case MediaCommand.ToggleShuffle:
                    var currentShuffle = CurrentState.IsShuffleActive;
                    await player.SetShuffleAsync(!currentShuffle).ConfigureAwait(false);
                    break;
                case MediaCommand.ToggleRepeat:
                    var nextRepeat = CurrentState.RepeatMode switch
                    {
                        "None" => "Playlist",
                        "List" => "Track",
                        "Track" => "None",
                        _ => "None"
                    };
                    await player.SetLoopStatusAsync(nextRepeat).ConfigureAwait(false);
                    break;
            }
        }
        private async Task SwitchActivePlayerAsync(CancellationToken cancellationToken)
        {
            propertiesWatch?.Dispose();
            propertiesWatch = null;

            if (dBus == null || selectedPlayer == null)
            {
                ResetState();
                return;
            }

            var proxy = new Player(dBus, selectedPlayer, PlayerPath);

            propertiesWatch = await proxy.WatchPropertiesChangedAsync(
                changed => _ = OnPropertiesChangedAsync(changed),
                emitOnCapturedContext: false).ConfigureAwait(false);

            await RebuildStateAsync(proxy, cancellationToken).ConfigureAwait(false);
        }
        private async Task OnPropertiesChangedAsync(IChangedPlayerProperties _)
        {
            if (dBus == null || selectedPlayer == null) return;
            var proxy = new Player(dBus, selectedPlayer, PlayerPath);
            await RebuildStateAsync(proxy, cts.Token).ConfigureAwait(false);
        }
        private async Task RebuildStateAsync(Player proxy, CancellationToken cancellationToken)
        {
            try
            {
                var metadata = await proxy.GetMetadataAsync().ConfigureAwait(false);
                var playbackStatus = await proxy.GetPlaybackStatusAsync().ConfigureAwait(false);
                var loopStatus = await SafeGetAsync(proxy.GetLoopStatusAsync, "None").ConfigureAwait(false);
                var shuffle = await SafeGetAsync(proxy.GetShuffleAsync, false).ConfigureAwait(false);
                var positionUs = await SafeGetAsync(proxy.GetPositionAsync, 0L).ConfigureAwait(false);

                lastKnownPositionSeconds = positionUs / UsToSeconds;
                lastKnownPositionUpdatedUtc = DateTimeOffset.UtcNow;

                var title = GetMetadataString(metadata, "xesam:title");
                var album = GetMetadataString(metadata, "xesam:album");
                var artist = metadata.TryGetValue("xesam:artist", out var artistValue)
                    && artistValue.Type == VariantValueType.Array
                        ? string.Join(", ", artistValue.GetArray<string>())
                        : string.Empty;

                var durationSeconds = metadata.TryGetValue("mpris:length", out var lengthValue)
                    && lengthValue.Type == VariantValueType.Int64
                        ? lengthValue.GetInt64() / UsToSeconds
                        : 0f;

                var blobs = new Dictionary<string, byte[]>();
                var artUrl = GetMetadataString(metadata, "mpris:artUrl");
                if (!artUrl.IsNullOrEmpty())
                {
                    var art = await TryFetchArtAsync(artUrl, cancellationToken).ConfigureAwait(false);
                    if (art is { Length: > 0 })
                        blobs[BlobKeys.Thumbnail] = art;
                }

                var friendlyNames = new Dictionary<string, string>();
                foreach (var player in knownPlayers)
                    friendlyNames[player] = await ResolveFriendlyNameAsync(player).ConfigureAwait(false);

                CurrentState = new MediaState
                {
                    Title = title,
                    Artist = artist,
                    Album = album,

                    PlaybackStatus = playbackStatus,
                    PositionSeconds = lastKnownPositionSeconds,
                    DurationSeconds = durationSeconds,
                    PositionLastUpdatedUtc = lastKnownPositionUpdatedUtc,
                    SelectedAppId = selectedPlayer,
                    AvailableAppIds = [.. knownPlayers],
                    AppFriendlyNames = friendlyNames,

                    SupportsShuffling = true, // MPRIS doesn't provide a way to query this, assume true
                    SupportsRepeat = true,
                    SupportsStop = true,
                    IsShuffleActive = shuffle,
                    RepeatMode = loopStatus switch { "Track" => "Track", "Playlist" => "List", _ => "None" },
                    HasThumbnail = blobs.ContainsKey(BlobKeys.Thumbnail)
                };
                CurrentBlobs = blobs;
                NotifyStateUpdated();
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Failed to rebuild MPRIS state");
            }
        }
        private async Task PositionPollLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PositionPollMs, cancellationToken).ConfigureAwait(false);
                if (dBus == null || selectedPlayer == null || !IsConnected) continue;

                try
                {
                    var proxy = new Player(dBus, selectedPlayer, PlayerPath);
                    var positionUs = await proxy.GetPositionAsync().ConfigureAwait(false);
                    lastKnownPositionSeconds = positionUs / UsToSeconds;
                    lastKnownPositionUpdatedUtc = DateTimeOffset.UtcNow;

                    CurrentState.PositionSeconds = lastKnownPositionSeconds;
                    CurrentState.PositionLastUpdatedUtc = lastKnownPositionUpdatedUtc;
                    NotifyStateUpdated();
                }
                catch { /* Player may have disappeared, ignore */ }
            }
        }
        private async Task<string> ResolveFriendlyNameAsync(string busName)
        {
            if (dBus == null) return busName[MprisPrefix.Length..];
            try
            {
                var root = new MediaPlayer2(dBus, busName, PlayerPath);
                var identity = await root.GetIdentityAsync().ConfigureAwait(false);
                if (identity.IsNullOrEmpty()) return identity;
            }
            catch { }

            var suffix = busName[MprisPrefix.Length..];
            var dot = suffix.IndexOf('.');
            var trimmed = dot > 0 ? suffix[..dot] : suffix;
            return trimmed.Length > 0 ? char.ToUpper(trimmed[0]) + trimmed[1..] : suffix;
        }
        private async Task<byte[]?> TryFetchArtAsync(string artUrl, CancellationToken cancellationToken)
        {
            try
            {
                var uri = new Uri(artUrl);
                if (uri.Scheme is "http" or "https")
                    return await httpClient.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);

                if (uri.Scheme == "file")
                {
                    var winePath = "Z:" + uri.LocalPath.Replace('/', '\\');
                    if (File.Exists(winePath))
                        return await File.ReadAllBytesAsync(winePath, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"Could not fetch art from {artUrl}");
            }
            return null;
        }
        private static string GetMetadataString(Dictionary<string, VariantValue> metadata, string key) =>
            metadata.TryGetValue(key, out var value) && value.Type == VariantValueType.String
                ? value.GetString()
                : string.Empty;
        private static async Task<T> SafeGetAsync<T>(Func<Task<T>> getter, T fallback)
        {
            try { return await getter().ConfigureAwait(false); }
            catch { return fallback; }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                propertiesWatch?.Dispose();
                nameOwnerWatch?.Dispose();
                dBus?.Dispose();
                sessionLock.Dispose();
                httpClient.Dispose();
            }
        }
    }
}
