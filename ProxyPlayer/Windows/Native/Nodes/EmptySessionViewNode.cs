using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace ProxyPlayer.Windows.Native.Nodes;

public sealed class EmptySessionViewNode : ResNode
{
    private const string NoSessionText = "No media session active";
    private const string SelectSessionButtonText = "Select Music Source";

    public EmptySessionViewNode(Vector2 contentSize)
    {
        Size = contentSize;

        var messageNode = new TextNode
        {
            String = NoSessionText,
            AlignmentType = AlignmentType.Center,
            FontSize = 14,
            Height = 22f
        };

        var selectSourceButton = new TextButtonNode
        {
            String = SelectSessionButtonText,
            Width = 170f,
            Height = 28f,
            OnClick = () => OnSelectSession?.Invoke(),
        };

        var buttonRow = new HorizontalFlexNode
        {
            Height = 28f,
            AlignmentFlags = FlexFlags.CenterHorizontally | FlexFlags.CenterVertically,
            InitialNodes = [selectSourceButton]
        };

        new VerticalListNode
        {
            FitWidth = true,
            Alignment = VerticalListAlignment.Left,
            ItemSpacing = 5f,
            Size = contentSize,
            InitialNodes =
            [
                new ResNode { Height = contentSize.Y / 3 },
                messageNode,
                buttonRow
            ]
        }.AttachNode(this);
    }

    public Action? OnSelectSession { get; init; }
}
