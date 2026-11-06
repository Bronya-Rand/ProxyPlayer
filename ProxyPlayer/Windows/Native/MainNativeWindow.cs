using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;
using ProxyPlayer.Shared;
using ProxyPlayer.Windows.Native.Layout;
using ProxyPlayer.Windows.Native.Nodes;

namespace ProxyPlayer.Windows.Native;

public sealed class MainNativeWindow : NativeAddon
{
    private readonly IMediaSource mediaSource;

    // Media Control Nodes
    private readonly Dictionary<DisplayLayout, LayoutResNode> playerLayouts = [];
    private readonly Plugin plugin;
    private readonly SessionNativeModal sessionListModal;
    private ResNode contentRoot = null!;
    private byte[]? coverArtBytes;
    private int coverArtGeneration;

    // Nodes
    private DisconnectedViewNode disconnectedNode = null!;
    private EmptySessionViewNode emptySessionNode = null!;
    private bool nodesReady;

    [SetsRequiredMembers]
    public MainNativeWindow(Plugin plugin, IMediaSource mediaSource)
    {
        Title = "ProxyPlayer";
        InternalName = "ProxyPlayerNowPlaying";

        this.plugin = plugin;
        this.mediaSource = mediaSource;
        sessionListModal = new SessionNativeModal(mediaSource)
        {
            OnSelectSessionId = sessionId => _ =
                                                 mediaSource.SelectSessionAsync(sessionId)
        };
    }

    private LayoutResNode ActiveLayout =>
        playerLayouts[plugin.Configuration.PlayerDisplayLayout];

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        base.OnSetup(addon, atkValueSpan);

        SetWindowSize(new Vector2(420f, 220f));
        playerLayouts[DisplayLayout.Compact] = new CompactPlayerNode(mediaSource, ContentSize)
        {
            OnRequestSessionList = () => sessionListModal.Open()
        };

        SetWindowSize(new Vector2(420f, 420f));
        playerLayouts[DisplayLayout.Portrait] = new PortraitPlayerNode(mediaSource, ContentSize)
        {
            OnRequestSessionList = () => sessionListModal.Open()
        };

        SetWindowSize(ActiveLayout.PreferredWindowSize);
        disconnectedNode = new DisconnectedViewNode(ContentSize);
        emptySessionNode = new EmptySessionViewNode(ContentSize)
        {
            OnSelectSession = () =>
                sessionListModal.Open()
        };

        contentRoot = new ResNode
        {
            Position = ContentStartPosition,
            Size = ContentSize,
        };
        contentRoot.AttachNode(this);

        disconnectedNode.AttachNode(contentRoot);
        emptySessionNode.AttachNode(contentRoot);
        playerLayouts[DisplayLayout.Portrait].AttachNode(contentRoot);
        playerLayouts[DisplayLayout.Compact].AttachNode(contentRoot);

        nodesReady = true;
        plugin.Configuration.OnDisplayLayoutChanged += HandleDisplayLayoutChanged;
        mediaSource.OnStateUpdated += HandleStateUpdated;

        ApplyState(mediaSource.CurrentState);
        RefreshCoverArt();
    }

    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        base.OnUpdate(addon);

        if (nodesReady && ActiveLayout.IsVisible)
            ActiveLayout.UpdateProgress(mediaSource.CurrentState);
    }

    protected override unsafe void OnHide(AtkUnitBase* addon)
    {
        InvalidateNodes();
        base.OnHide(addon);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        InvalidateNodes();
        playerLayouts.Clear();
        base.OnFinalize(addon);
    }

    private void InvalidateNodes()
    {
        mediaSource.OnStateUpdated -= HandleStateUpdated;
        plugin.Configuration.OnDisplayLayoutChanged -= HandleDisplayLayoutChanged;
        nodesReady = false;
        coverArtGeneration++;
        coverArtBytes = null;
    }

    private void ApplyState(MediaState state)
    {
        var isDisconnected = mediaSource is { IsConnected: false, SourceName: "Windows SMTC" };
        var hasNoSession = !isDisconnected && state.SelectedAppId.IsNullOrEmpty();
        var isPlaying = !isDisconnected && !hasNoSession;
        var currentLayoutType = plugin.Configuration.PlayerDisplayLayout;

        SetWindowSize((int)ActiveLayout.PreferredWindowSize.X, (int)ActiveLayout.PreferredWindowSize.Y);
        contentRoot.Position = ContentStartPosition;
        contentRoot.Size = ContentSize;
        disconnectedNode.Size = ContentSize;
        emptySessionNode.Size = ContentSize;

        disconnectedNode.IsVisible = isDisconnected;
        emptySessionNode.IsVisible = hasNoSession;

        foreach (var (layoutType, layoutNode) in playerLayouts)
            layoutNode.IsVisible = isPlaying && layoutType == currentLayoutType;

        if (isPlaying)
            ActiveLayout.State = state;
    }

    private void RefreshCoverArt()
    {
        var state = mediaSource.CurrentState;
        var bytes = state.HasThumbnail && mediaSource.TryGetThumbnail(out var thumbnail) && thumbnail.Length > 0
                        ? thumbnail
                        : null;

        if (bytes is null && coverArtBytes is null) return;
        if (bytes is not null && coverArtBytes is not null && bytes.AsSpan().SequenceEqual(coverArtBytes)) return;

        coverArtBytes = (byte[])bytes?.Clone()!;
        var generation = ++coverArtGeneration;

        foreach (var layout in playerLayouts.Values)
        {
            layout.CoverArt = null;
            if (coverArtBytes is not null)
                _ = LoadCoverArtAsync(layout, coverArtBytes, generation);
        }
    }

    private async Task LoadCoverArtAsync(LayoutResNode layout, byte[] bytes, int generation)
    {
        IDalamudTextureWrap? wrap = null;
        try
        {
            // Each image node owns its own wrapper; never borrow one from TextureCache.
            wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes);
            await Plugin.Framework.Run(() =>
            {
                if (!nodesReady || generation != coverArtGeneration) return;

                layout.CoverArt = wrap;
                wrap = null; // Ownership transferred to the native image node.
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to load native cover art");
        } finally
        {
            wrap?.Dispose();
        }
    }

    private void HandleStateUpdated() => _ = ApplyStateUpdateAsync();

    private void HandleDisplayLayoutChanged(DisplayLayout newLayout)
    {
        if (!nodesReady) return;
        Plugin.Framework.Run(() =>
        {
            if (!nodesReady) return;
            ApplyState(mediaSource.CurrentState);
        });
    }

    private async Task ApplyStateUpdateAsync()
    {
        try
        {
            await Plugin.Framework.Run(() =>
            {
                if (!nodesReady) return;

                ApplyState(mediaSource.CurrentState);
                RefreshCoverArt();
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to update native player state");
        }
    }

    public void CloseAddon()
    {
        sessionListModal.Close();
        this.Close();
    }
}
