using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;

namespace ProxyPlayer.Windows.Native;

/// <summary>
/// Displays the list of available media sessions from the ProxyPlayer server.
/// </summary>
public sealed class SessionNativeModal : NativeAddon
{
    private readonly IMediaSource mediaSource;
    private MediaState? previousState;
    private DropDownNode<string>? sourcesDropDown;

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
        SetWindowSize(300f, 90f);
        previousState = null;
        sourcesDropDown = new DropDownNode<string>
        {
            NavIndex = 1,
            Position = ContentStartPosition,
            Width = ContentSize.X,
            Height = 24f,
            PlaceholderString = "No active music sources found.",
            GetLabelFunction = appId => mediaSource.CurrentState.AppFriendlyNames.GetValueOrDefault(appId, appId),
            OnOptionSelected = appId => OnSelectSessionId?.Invoke(appId),
        };
        sourcesDropDown.AttachNode(this);
        RefreshSources();
    }

    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        base.OnUpdate(addon);
        RefreshSources();
    }

    protected override unsafe void OnHide(AtkUnitBase* addon)
    {
        sourcesDropDown = null;
        base.OnHide(addon);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        sourcesDropDown = null;
        base.OnFinalize(addon);
    }

    private void RefreshSources()
    {
        if (sourcesDropDown is not { } dropDown) return;
        var state = mediaSource.CurrentState;
        if (ReferenceEquals(previousState, state)) return;

        dropDown.PlaceholderString = state.AvailableAppIds.Length > 0
                                         ? "Select a music source."
                                         : "No active music sources found.";
        if (!dropDown.Options.SequenceEqual(state.AvailableAppIds)
            || previousState is null
            || previousState.AppFriendlyNames.Count != state.AppFriendlyNames.Count
            || state.AppFriendlyNames.Any(pair => !previousState.AppFriendlyNames.TryGetValue(pair.Key, out var name)
                                                  || name != pair.Value))
            dropDown.Options = [.. state.AvailableAppIds];

        dropDown.IsEnabled = state.AvailableAppIds.Length > 0;
        dropDown.SelectedOption = state.AvailableAppIds.Contains(state.SelectedAppId) ? state.SelectedAppId : null;
        previousState = state;
    }
}
