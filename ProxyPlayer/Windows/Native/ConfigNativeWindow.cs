using System;
using System.Diagnostics.CodeAnalysis;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows.Native;

public sealed class ConfigNativeWindow : NativeAddon
{
    private readonly Configuration configuration;

    [SetsRequiredMembers]
    public ConfigNativeWindow(Plugin plugin)
    {
        Title = "ProxyPlayer Configuration";
        InternalName = "ProxyPlayerConfig";

        configuration = plugin.Configuration;
    }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        base.OnSetup(addon, atkValueSpan);
        SetWindowSize(300, 120);

        new VerticalListNode // Main
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            FitWidth = true,
            Height = 22f,
            ItemSpacing = 5f,
            InitialNodes =
            [
                new HorizontalFlexNode // Body
                {
                    Height = 22f,
                    AlignmentFlags = FlexFlags.FitWidth | FlexFlags.FitHeight | FlexFlags.CenterHorizontally,
                    InitialNodes =
                    [
                        new TextNode
                        {
                            String = "Player Layout",
                        },
                        new DropDownNode<DisplayLayout> // Display Layout
                        {
                            NavIndex = 1,
                            NavDown = 3,
                            Width = ContentSize.X,
                            GetLabelFunction = layout => layout.ToFriendlyString(),
                            Options = [.. Enum.GetValues<DisplayLayout>()],
                            SelectedOption = configuration.PlayerDisplayLayout,
                            OnOptionSelected = SetDisplayLayout,
                            TextTooltip = "Set the player layout that ProxyPlayer should use when the window is open."
                        }
                    ]
                },
                new CheckboxNode // KTK Toggle
                {
                    String = "Use Native UI over ImGui",
                    NavIndex = 3,
                    NavUp = 1,
                    Width = ContentSize.X,
                    Height = 22f,
                    TextTooltip = "Toggle the use of Native UI over ImGui in ProxyPlayer.",
                    IsChecked = configuration.UseNative,
                    OnClick = value =>
                    {
                        configuration.UseNative = value;
                        configuration.Save();
                    }
                }
            ]
        }.AttachNode(this);
    }

    private void SetDisplayLayout(DisplayLayout layout)
    {
        configuration.PlayerDisplayLayout = layout;
        configuration.Save();
    }
}
