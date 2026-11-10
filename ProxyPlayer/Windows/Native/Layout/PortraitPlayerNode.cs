using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows.Native.Layout;

/// <summary>
/// Renders the player in a portrait layout.
/// </summary>
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

    /// <inheritdoc />
    public override Vector2 PreferredWindowSize => new(420f, 420f);

    /// <inheritdoc />
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
            ItemSpacing = 6f,
            AlignmentFlags = FlexFlags.FitHeight | FlexFlags.CenterHorizontally,
            InitialNodes = [new ResNode { Width = Size.X / 4 }, SessionNameNode, SourcesButton]
        };

        // Cover Art
        var coverRow = new HorizontalFlexNode
        {
            Width = Size.X,
            Height = 180f,
            AlignmentFlags = FlexFlags.CenterHorizontally | FlexFlags.CenterVertically,
            InitialNodes = [CoverArtNode]
        };

        TimeRow.Width = Size.X;
        ControlsRow.Width = Size.X;
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
                TimeRow,
                ControlsRow
            ]
        };
    }
}
