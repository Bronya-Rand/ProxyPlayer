using System.Numerics;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows.Native.Layout;

public sealed class CompactPlayerNode : LayoutResNode
{
    public CompactPlayerNode(IMediaSource mediaSource, Vector2 contentSize) : base(mediaSource)
    {
        Size = contentSize;

        CoverArtNode.Size = new Vector2(85f, 85f);
        SourcesButton.Width = 65f;

        InitializeLayout();
    }

    public override Vector2 PreferredWindowSize => new(420f, 220f);

    protected override NodeBase BuildLayout()
    {
        const float buttonWidth = 80f;
        const float buttonHeight = 26f;
        var metadataWidth = Size.X - buttonWidth - 8f;

        // Session Button / Name
        SourcesButton.Width = buttonWidth;
        SourcesButton.Height = buttonHeight;
        SessionNameNode.Width = metadataWidth - buttonWidth - 6f;
        SessionNameNode.Height = buttonHeight;

        var sessionRow = new HorizontalListNode
        {
            Width = metadataWidth,
            Height = buttonHeight,
            ItemSpacing = 4f,
            InitialNodes = [SessionNameNode, SourcesButton]
        };

        // Metadata: Session, Title -> Album
        var metadataColumn = new VerticalListNode
        {
            Width = metadataWidth,
            FitWidth = true,
            ItemSpacing = 2f,
            InitialNodes = [sessionRow, TitleNode, ArtistNode, AlbumNode]
        };

        var headerRow = new HorizontalListNode
        {
            Width = Size.X,
            Height = 85f,
            FitHeight = false,
            ItemSpacing = 6f,
            InitialNodes = [CoverArtNode, metadataColumn]
        };

        // Progress / Duration / Progress Bar
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
            Height = 28f,
            ItemSpacing = 6f,
            AlignmentFlags = FlexFlags.FitHeight | FlexFlags.CenterHorizontally,
            InitialNodes = [ShuffleButton, PrevButton, PlayPauseButton, NextButton, RepeatButton]
        };

        return new VerticalListNode
        {
            Width = Size.X,
            FitWidth = true,
            ItemSpacing = 6f,
            InitialNodes = [headerRow, ProgressBarNode, timeRow, controlsRow]
        };
    }
}
