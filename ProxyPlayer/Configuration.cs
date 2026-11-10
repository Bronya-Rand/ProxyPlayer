using System;
using Dalamud.Configuration;
using ProxyPlayer.Models;

namespace ProxyPlayer;

[Serializable]
public class Configuration : IPluginConfiguration
{
    private DisplayLayout playerDisplayLayout = DisplayLayout.Compact;

    private bool useNative = true;

    public DisplayLayout PlayerDisplayLayout
    {
        get => playerDisplayLayout;
        set
        {
            if (playerDisplayLayout == value) return;
            playerDisplayLayout = value;
            OnDisplayLayoutChanged?.Invoke(value);
        }
    }

    public bool UseNative
    {
        get => useNative;
        set
        {
            if (useNative == value) return;
            useNative = value;
            OnUseNativeChanged?.Invoke(value);
        }
    }

    public int Version { get; set; } = 1;

    [field: NonSerialized]
    public event Action<DisplayLayout>? OnDisplayLayoutChanged;

    [field: NonSerialized]
    public event Action<bool>? OnUseNativeChanged;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
