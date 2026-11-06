using System;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;
using ProxyPlayer.Utility;

namespace ProxyPlayer.Windows.Native.Layout;

public abstract class LayoutResNode : ResNode
{
    protected readonly TextNode AlbumNode;
    protected readonly TextNode ArtistNode;

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
    protected readonly TextNode TitleNode;
    protected LayoutListNode RootLayout = null!;

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
            { String = "Play", Width = 80f, Height = 26f, OnClick = () => MediaSource.PlayPauseAsync() };
        ShuffleButton = new TextButtonNode
            { String = "Shuffle", Width = 80f, Height = 26f, OnClick = () => _ = MediaSource.ToggleShuffleAsync() };
        RepeatButton = new TextButtonNode
            { String = "Repeat", Width = 80f, Height = 26f, OnClick = () => _ = MediaSource.ToggleRepeatAsync() };
        PrevButton = new TextButtonNode
            { String = "< Prev", Width = 80f, Height = 26f, OnClick = () => _ = MediaSource.PreviousAsync() };
        NextButton = new TextButtonNode
            { String = "Next >", Width = 80f, Height = 26f, OnClick = () => _ = MediaSource.NextAsync() };
    }

    public Action? OnRequestSessionList { get; init; }
    public abstract Vector2 PreferredWindowSize { get; }

    public MediaState State
    {
        set
        {
            SessionNameNode.String = TrackMetadata.GetFriendlyAppName(value);
            TitleNode.String = value.Title.IsNullOrEmpty() ? "Unknown Title" : value.Title;
            ArtistNode.String = value.Artist.IsNullOrEmpty() ? "Unknown Artist" : value.Artist;
            AlbumNode.IsVisible = !value.Album.IsNullOrEmpty();
            if (AlbumNode.IsVisible) AlbumNode.String = value.Album;
            //AlbumNode.String = "Test";

            DurationTimeNode.String = $"{TimeSpan.FromSeconds(value.DurationSeconds):mm\\:ss}";
            PlayPauseButton.String = value.PlaybackStatus == "Playing" ? "Pause" : "Play";

            ShuffleButton.IsVisible = value.SupportsShuffling;
            RepeatButton.IsVisible = value.SupportsRepeat;

            RootLayout.RecalculateLayout();
        }
    }

    /// <summary>Transfers ownership of a new, unshared texture to the image node.</summary>
    public IDalamudTextureWrap? CoverArt
    {
        set
        {
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

            RootLayout.RecalculateLayout();
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
    /// Derived layouts
    /// </summary>
    /// <returns></returns>
    protected abstract NodeBase BuildLayout();

    public void UpdateProgress(MediaState state)
    {
        if (state.DurationSeconds <= 0) return;
        var currentPos = TrackMetadata.GetInterpolatedPosition(state);
        ProgressBarNode.Progress = Math.Clamp((float)(currentPos / state.DurationSeconds), 0f, 1f);
        PositionTimeNode.String = $"{TimeSpan.FromSeconds(currentPos):mm\\:ss}";
    }
}
