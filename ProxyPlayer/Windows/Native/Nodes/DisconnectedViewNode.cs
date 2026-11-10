using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace ProxyPlayer.Windows.Native.Nodes;

public sealed class DisconnectedViewNode : ResNode
{
    private const string ServerNotRunningText = "Not connected to ProxyPlayer server";
    private readonly VerticalListNode? layout;

    public DisconnectedViewNode(Vector2 contentSize)
    {
        Size = contentSize;

        var textNode = new TextNode
        {
            String = ServerNotRunningText,
            TextColor = ColorHelper.GetColor(32),
            AlignmentType = AlignmentType.Center,
            FontSize = 14,
            Height = 24f,
        };

        layout = new VerticalListNode
        {
            FitWidth = true,
            Alignment = VerticalListAlignment.Left,
            ItemSpacing = 8f,
            Size = contentSize,
            InitialNodes =
            [
                new ResNode { Height = contentSize.Y / 3 },
                textNode,
            ]
        };
        layout.AttachNode(this);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        if (layout is null) return;
        layout.Size = Size;
        layout.Nodes[0].Height = Height / 3;
        layout.RecalculateLayout();
    }
}
