using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ProxyPlayer;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("ProxyPlayer Configuration###ProxyPlayerConfig")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 80);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() => GC.SuppressFinalize(this);

    public override void Draw()
    {
        var displayLayout = configuration.PlayerDisplayLayout;
        var displayLayoutPlaceholder = displayLayout.ToFriendlyString();
        ImGui.Text("Player Layout:");
        using (var displayCombo = ImRaii.Combo("###ProxyPlayerDisplayLayout", displayLayoutPlaceholder))
        {
            if (displayCombo)
            {
                foreach (var layout in Enum.GetValues<DisplayLayout>())
                {
                    if (ImGui.Selectable(layout.ToFriendlyString(), layout == displayLayout))
                    {
                        configuration.PlayerDisplayLayout = layout;
                        configuration.Save();
                    }
                }
            }
        }
    }
}
