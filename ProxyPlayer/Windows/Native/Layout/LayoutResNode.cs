using System;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;
using ProxyPlayer.Utility;

namespace ProxyPlayer.Windows.Native.Layout;

public abstract class LayoutResNode : ResNode
{
    protected readonly TextNode AlbumNode;
    protected readonly TextNode ArtistNode;
    protected readonly HorizontalFlexNode ControlsRow;

    // ATK Nodes
    protected readonly ImGuiImageNode CoverArtNode;
    protected readonly TextNode DurationTimeNode;
    protected readonly IMediaSource MediaSource;
    protected readonly TextButtonNode NextButton;

    protected readonly TextButtonNode PlayPauseButton;
    protected readonly TextNode PositionTimeNode;
    protected readonly TextButtonNode PrevButton;

    protected readonly ProgressBarNode ProgressBarNode;
    protected readonly TextButtonNode RepeatButton;
    protected readonly TextNode SessionNameNode;
    protected readonly TextButtonNode ShuffleButton;
    protected readonly TextButtonNode SourcesButton;
    protected readonly HorizontalFlexNode TimeRow;
    protected readonly TextNode TitleNode;
    protected LayoutListNode RootLayout = null!;
    private long displayedPositionSeconds = -1;

    protected LayoutResNode(IMediaSource mediaSource)
    {
        MediaSource = mediaSource;

        // Main Nodes
        CoverArtNode = new ImGuiImageNode { FitTexture = true, IsVisible = false };
        SessionNameNode = new TextNode
        {
            FontSize = 12,
            TextColor = new Vector4(0.7f, 0.7f, 0.7f, 1f),
            Height = 16f,
            TextFlags = TextFlags.Ellipsis
        };
        TitleNode = new TextNode
        {
            FontSize = 15,
            Height = 20f,
            TextFlags = TextFlags.Ellipsis
        };
        ArtistNode = new TextNode
        {
            FontSize = 13,
            TextColor = new Vector4(0.7f, 0.7f, 0.7f, 1f),
            Height = 18f,
            TextFlags = TextFlags.Ellipsis
        };
        AlbumNode = new TextNode
        {
            FontSize = 12,
            TextColor = new Vector4(0.7f, 0.7f, 0.7f, 1f),
            Height = 16f,
            TextFlags = TextFlags.Ellipsis
        };

        // Progress / Duration
        ProgressBarNode = new ProgressBarNode { Height = 8f };
        PositionTimeNode = new TextNode { String = "00:00", AlignmentType = AlignmentType.TopLeft, Height = 16f };
        DurationTimeNode = new TextNode { String = "00:00", AlignmentType = AlignmentType.TopRight, Height = 16f };

        // Media Controls
        SourcesButton = new TextButtonNode
        {
            String = "Sources",
            Height = 24f,
            OnClick = () => OnRequestSessionList?.Invoke()
        };
        PlayPauseButton = new TextButtonNode
            { String = "Play", Width = 72f, Height = 26f, OnClick = () => _ = MediaSource.PlayPauseAsync() };
        ShuffleButton = new TextButtonNode
            { String = "Shuffle", Width = 72f, Height = 26f, OnClick = () => _ = MediaSource.ToggleShuffleAsync() };
        RepeatButton = new TextButtonNode
            { String = "Repeat", Width = 72f, Height = 26f, OnClick = () => _ = MediaSource.ToggleRepeatAsync() };
        PrevButton = new TextButtonNode
            { String = "< Prev", Width = 72f, Height = 26f, OnClick = () => _ = MediaSource.PreviousAsync() };
        NextButton = new TextButtonNode
            { String = "Next >", Width = 72f, Height = 26f, OnClick = () => _ = MediaSource.NextAsync() };

        TimeRow = new HorizontalFlexNode
        {
            Width = 400f,
            Height = 16f,
            AlignmentFlags = FlexFlags.FitWidth | FlexFlags.CenterVertically,
            InitialNodes = [PositionTimeNode, DurationTimeNode]
        };
        ControlsRow = new HorizontalFlexNode
        {
            Height = 28f,
            AlignmentFlags = FlexFlags.CenterHorizontally | FlexFlags.CenterVertically,
            InitialNodes =
            [
                new HorizontalListNode
                {
                    Height = 26f,
                    ItemSpacing = 6f,
                    FitToContentWidth = true,
                    InitialNodes = [ShuffleButton, PrevButton, PlayPauseButton, NextButton, RepeatButton]
                }
            ]
        };
    }

    /// <summary>
    /// Opens the Session modal.
    /// </summary>
    public Action? OnRequestSessionList { get; init; }

    /// <summary>
    /// Sets the window size of the window.
    /// </summary>
    public abstract Vector2 PreferredWindowSize { get; }

    /// <summary>
    /// Updates the metadata and player abilities on song/player change.
    /// </summary>
    public MediaState State
    {
        set
        {
            var layoutChanged = AlbumNode.IsVisible != !value.Album.IsNullOrEmpty()
                                || ShuffleButton.IsVisible != value.SupportsShuffling
                                || RepeatButton.IsVisible != value.SupportsRepeat;
            SessionNameNode.String = TrackMetadata.GetFriendlyAppName(value);
            TitleNode.String = value.Title.IsNullOrEmpty() ? "Unknown Title" : value.Title;
            ArtistNode.String = value.Artist.IsNullOrEmpty() ? "Unknown Artist" : value.Artist;
            AlbumNode.IsVisible = !value.Album.IsNullOrEmpty();
            if (AlbumNode.IsVisible) AlbumNode.String = value.Album;
            DurationTimeNode.String = FormatTime(value.DurationSeconds);
            PlayPauseButton.String = value.PlaybackStatus == "Playing" ? "Pause" : "Play";

            ShuffleButton.IsVisible = value.SupportsShuffling;
            RepeatButton.IsVisible = value.SupportsRepeat;
            ShuffleButton.TextTooltip = value.IsShuffleActive ? "Shuffle: On" : "Shuffle: Off";
            RepeatButton.TextTooltip = value.RepeatMode switch
            {
                "Track" => "Repeat: Track",
                "List" => "Repeat: All",
                _ => "Repeat: Off"
            };

            UpdateProgress(value);
            if (layoutChanged) RootLayout.RecalculateLayout();
        }
    }

    /// <summary>
    /// Handles updating the cover art on song/player switching with ownership.
    /// </summary>
    public IDalamudTextureWrap? CoverArt
    {
        set
        {
            var wasVisible = CoverArtNode.IsVisible;
            if (value != null)
            {
                CoverArtNode.TextureSize = value.Size;
                CoverArtNode.LoadTexture(value);
                CoverArtNode.IsVisible = true;
            }
            else
            {
                CoverArtNode.IsVisible = false;
            }

            if (wasVisible != CoverArtNode.IsVisible) RootLayout.RecalculateLayout();
        }
    }

    /// <summary>
    /// Called by the derived class ctor after customizing node sizes.
    /// </summary>
    protected void InitializeLayout()
    {
        RootLayout = (LayoutListNode)BuildLayout();
        RootLayout.AttachNode(this);
    }

    /// <summary>
    /// Builds the UI for the ResNode from a derived class.
    /// </summary>
    /// <returns></returns>
    protected abstract NodeBase BuildLayout();

    public void UpdateProgress(MediaState state)
    {
        var duration = double.IsFinite(state.DurationSeconds) && state.DurationSeconds > 0
                           ? state.DurationSeconds
                           : 0;
        var currentPos = duration > 0 ? (double)TrackMetadata.GetInterpolatedPosition(state) : 0;
        currentPos = double.IsFinite(currentPos) ? Math.Clamp(currentPos, 0, duration) : 0;
        ProgressBarNode.Progress = duration > 0 ? (float)(currentPos / duration) : 0;

        var seconds = (long)Math.Min(currentPos, TimeSpan.MaxValue.TotalSeconds - 1);
        if (displayedPositionSeconds == seconds) return;
        displayedPositionSeconds = seconds;
        PositionTimeNode.String = FormatTime(seconds);
    }

    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return "00:00";
        var time = TimeSpan.FromSeconds(Math.Min(Math.Floor(seconds), TimeSpan.MaxValue.TotalSeconds - 1));
        return time.TotalHours >= 1
                   ? $"{(long)time.TotalHours}:{time:mm\\:ss}"
                   : $"{time:mm\\:ss}";
    }
}
