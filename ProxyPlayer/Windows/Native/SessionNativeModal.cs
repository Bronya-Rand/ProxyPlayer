using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows.Native;

public sealed class SessionNativeModal : NativeAddon
{
    private readonly IMediaSource mediaSource;

    [SetsRequiredMembers]
    public SessionNativeModal(IMediaSource mediaSource)
    {
        Title = "Select Music Source";
        InternalName = "ProxyPlayerSessionList";

        this.mediaSource = mediaSource;
    }

    public Action<string>? OnSelectSessionId { get; init; }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        base.OnSetup(addon, atkValueSpan);
        SetWindowSize(300f, 60f);

        new VerticalListNode // Main
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            FitWidth = true,
            InitialNodes = BuildNodes(),
        }.AttachNode(this);
    }

    private ICollection<NodeBase> BuildNodes()
    {
        var body = new HorizontalFlexNode
        {
            Height = 22f,
            AlignmentFlags = FlexFlags.FitWidth | FlexFlags.FitHeight | FlexFlags.CenterHorizontally,
        };

        var state = mediaSource.CurrentState;
        var apps = state.AvailableAppIds;
        if (apps.Length == 0)
        {
            new TextNode
            {
                Size = ContentSize,
                String = "No active music sources found.",
                TextColor = ColorHelper.GetColor(32),
                AlignmentType = AlignmentType.Center,
                FontSize = 14,
                Height = 24f,
            }.AttachNode(body);
        }
        else
        {
            new DropDownNode<string>
            {
                NavIndex = 1,
                Size = ContentSize,
                Height = 24f,
                GetLabelFunction = appId => state.AppFriendlyNames.GetValueOrDefault(appId, appId),
                Options = [.. apps],
                SelectedOption = state.SelectedAppId,
                OnOptionSelected = appId => OnSelectSessionId?.Invoke(appId),
            }.AttachNode(body);
        }

        return [body];
    }
}
