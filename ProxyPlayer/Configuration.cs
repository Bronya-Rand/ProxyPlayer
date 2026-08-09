using System;
using Dalamud.Configuration;
using ProxyPlayer.Models;

namespace ProxyPlayer;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public DisplayLayout PlayerDisplayLayout { get; set; } = DisplayLayout.Compact;
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
