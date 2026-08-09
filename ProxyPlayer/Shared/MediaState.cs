using System;
using System.Collections.Generic;

namespace SamplePlugin.Shared;

public enum MediaCommand
{
    PlayPause,
    Next,
    Previous,
    SelectSession,
    Stop,
    ToggleShuffle,
    ToggleRepeat
}

/// <summary>
/// The current state of the media session. Shared between the plugin and the server.
/// </summary>
public sealed class MediaState
{
    // Metadata
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public bool HasThumbnail { get; set; }

    // Playback state
    public string PlaybackStatus { get; set; } = "Closed";
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public DateTimeOffset PositionLastUpdatedUtc { get; set; }

    // Session Info
    public string? SelectedAppId { get; set; }
    public string[] AvailableAppIds { get; set; } = [];
    public Dictionary<string, string> AppFriendlyNames { get; set; } = [];

    // Capabilities
    public bool SupportsShuffling { get; set; }
    public bool SupportsRepeat { get; set; }
    public bool SupportsStop { get; set; }

    // Current Active States
    public bool IsShuffleActive { get; set; }
    public string RepeatMode { get; set; } = "None";
}

/// <summary>
/// Data structure for blobs.
/// </summary>
public static class BlobKeys
{
    public const string Thumbnail = "thumbnail";
}

public sealed class MediaCommandMessage
{
    public MediaCommand Command { get; set; }
    public string? TargetAppId { get; set; }
}

public sealed class MessageEnvelope<T>
{
    public required T Payload { get; init; }
    public Dictionary<string, byte[]> Blobs { get; init; } = [];

    public byte[]? GetBlob(string key) => Blobs.TryGetValue(key, out var blob) ? blob : null;
}
