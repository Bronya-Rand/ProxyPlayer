using System.Numerics;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows.Native.Layout;

/// <summary>
/// Renders the player in a compact layout.
/// </summary>
public sealed class CompactPlayerNode : LayoutResNode
{
    public CompactPlayerNode(IMediaSource mediaSource, Vector2 contentSize) : base(mediaSource)
    {
        Size = contentSize;

        CoverArtNode.Size = new Vector2(85f, 85f);
        InitializeLayout();
    }

    /// <inheritdoc />
    public override Vector2 PreferredWindowSize => new(420f, 220f);

    /// <inheritdoc />
    protected override NodeBase BuildLayout()
    {
        const float buttonWidth = 80f;
        const float buttonHeight = 26f;
        var metadataWidth = Size.X - CoverArtNode.Width - 6f;

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

        return new VerticalListNode
        {
            Width = Size.X,
            FitWidth = true,
            ItemSpacing = 6f,
            InitialNodes = [headerRow, ProgressBarNode, TimeRow, ControlsRow]
        };
    }
}
