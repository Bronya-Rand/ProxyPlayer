using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows.Native.Layout;

public sealed class PortraitPlayerNode : LayoutResNode
{
    public PortraitPlayerNode(IMediaSource mediaSource, Vector2 contentSize) : base(mediaSource)
    {
        Size = contentSize;
        CoverArtNode.Size = new Vector2(180f, 180f);

        SessionNameNode.AlignmentType = AlignmentType.Center;
        TitleNode.AlignmentType = AlignmentType.Center;
        ArtistNode.AlignmentType = AlignmentType.Center;
        AlbumNode.AlignmentType = AlignmentType.Center;

        InitializeLayout();
    }

    public override Vector2 PreferredWindowSize => new(420f, 420f);

    protected override NodeBase BuildLayout()
    {
        const float buttonWidth = 80f;
        const float buttonHeight = 26f;

        SourcesButton.Width = buttonWidth;
        SourcesButton.Height = buttonHeight;
        SessionNameNode.Width = Size.X - buttonWidth - 6f;
        SessionNameNode.Height = buttonHeight;

        // Session Name / Button
        var topRow = new HorizontalFlexNode
        {
            Width = Size.X,
            Height = buttonHeight,
            ItemSpacing = 2f,
            AlignmentFlags = FlexFlags.FitHeight | FlexFlags.CenterHorizontally,
            InitialNodes =
            [
                new ResNode { Width = Size.X / 4 },
                SessionNameNode,
                SourcesButton
            ]
        };

        // Cover Art
        var coverRow = new HorizontalFlexNode
        {
            Width = Size.X,
            Height = 180f,
            AlignmentFlags = FlexFlags.CenterHorizontally | FlexFlags.CenterVertically,
            InitialNodes = [CoverArtNode]
        };

        var timeRow = new HorizontalFlexNode
        {
            Width = Size.X,
            Height = 16f,
            AlignmentFlags = FlexFlags.FitWidth | FlexFlags.CenterVertically,
            InitialNodes = [PositionTimeNode, DurationTimeNode]
        };

        // Play / Pause / Prev / Next
        var controlsRow = new HorizontalFlexNode
        {
            Width = Size.X,
            Height = 28f,
            ItemSpacing = 6f,
            AlignmentFlags = FlexFlags.FitHeight | FlexFlags.CenterHorizontally,
            InitialNodes = [ShuffleButton, PrevButton, PlayPauseButton, NextButton, RepeatButton]
        };

        TitleNode.Width = Size.X;
        ArtistNode.Width = Size.X;
        AlbumNode.Width = Size.X;
        ProgressBarNode.Width = Size.X;

        return new VerticalListNode
        {
            Width = Size.X,
            ItemSpacing = 6f,
            InitialNodes =
            [
                topRow,
                coverRow,
                TitleNode,
                ArtistNode,
                AlbumNode,
                ProgressBarNode,
                timeRow,
                controlsRow
            ]
        };
    }
}
