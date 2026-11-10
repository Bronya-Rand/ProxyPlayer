using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ProxyPlayer.Models;

namespace ProxyPlayer.Windows.Legacy
{
    /// <summary>
    /// Displays the list of available media sessions from the ProxyPlayer server.
    /// </summary>
    /// <param name="mediaSource">The pipe client to use for communication with the ProxyPlayer server.</param>
    public sealed class SessionListModal(IMediaSource mediaSource)
    {
        private bool isOpen;
        private bool shouldOpenModal;
        public Action<string>? OnSelectSessionId { get; init; }

        public void Draw()
        {
            if (!isOpen) return;

            const string modalId = "Select Music Source###ProxyPlayerSessionList";
            if (shouldOpenModal)
            {
                ImGui.OpenPopup(modalId);
                shouldOpenModal = false;
            }

            var center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSizeConstraints(new Vector2(200, 75), new Vector2(float.MaxValue, 75));

            using var modal = ImRaii.PopupModal(modalId, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize);
            if (modal)
                DrawContent();
        }

        public void DrawContent()
        {
            var state = mediaSource.CurrentState;
            var apps = state.AvailableAppIds;
            if (apps.Length == 0)
            {
                ImGui.Text("No active music sources found.");
                return;
            }

            var selectedFriendlyName = state.SelectedAppId != null &&
                                       state.AppFriendlyNames.TryGetValue(state.SelectedAppId, out var sName)
                                           ? sName
                                           : state.SelectedAppId ?? "Select app...";
            using var mediaCombo = ImRaii.Combo("###ProxyPlayerSelectSession", selectedFriendlyName);
            if (mediaCombo)
            {
                foreach (var appId in apps)
                {
                    var appFriendlyName =
                        state.AppFriendlyNames.GetValueOrDefault(appId, appId);
                    if (ImGui.Selectable(appFriendlyName, appId == state.SelectedAppId))
                    {
                        OnSelectSessionId?.Invoke(appId);
                    }
                }
            }
        }

        public void Open()
        {
            isOpen = true;
            shouldOpenModal = true;
        }

        public void Close() => isOpen = false;
    }
}
