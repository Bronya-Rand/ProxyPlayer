using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ProxyPlayer.Media;

namespace ProxyPlayer.Windows
{
    /// <summary>
    /// Displays the list of available media sessions from the ProxyPlayer server.
    /// </summary>
    /// <param name="pipeClient">The pipe client to use for communication with the ProxyPlayer server.</param>
    public sealed class SessionListModal(PipeClient pipeClient)
    {
        private readonly PipeClient pipeClient = pipeClient;

        private bool shouldOpenModal;
        private bool isOpen;
        public Action<string>? OnSelectSessionId { get; set; }

        public void Draw()
        {
            if (!isOpen) return;

            var modalId = "Select Music Source###ProxyPlayerSessionList";
            if (shouldOpenModal)
            {
                ImGui.OpenPopup(modalId);
                shouldOpenModal = false;
            }

            var center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSizeConstraints(new Vector2(200, 100), new Vector2(float.MaxValue, 150));

            using var modal = ImRaii.PopupModal(modalId, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize);
            if (modal)
                DrawContent();
        }
        public void DrawContent()
        {
            var state = pipeClient.CurrentState;
            var apps = state.AvailableAppIds;
            if (apps.Length == 0)
            {
                ImGui.Text("No active music sources found.");
                return;
            }
            else
            {
                ImGui.Text("Select a music source:");
                var selectedFriendlyName = state.SelectedAppId != null && state.AppFriendlyNames.TryGetValue(state.SelectedAppId, out var sName)
                    ? sName
                    : state.SelectedAppId ?? "Select app...";
                using (var mediaCombo = ImRaii.Combo("###ProxyPlayerSelectSession", selectedFriendlyName))
                {
                    if (mediaCombo)
                    {
                        foreach (var appId in apps)
                        {
                            var appFriendlyName = state.AppFriendlyNames.TryGetValue(appId, out var fName) ? fName : appId;
                            if (ImGui.Selectable(appFriendlyName, appId == state.SelectedAppId))
                            {
                                OnSelectSessionId?.Invoke(appId);
                            }
                        }
                    }
                }
            }

            ImGui.Separator();

            if (ImGui.Button("Close"))
            {
                isOpen = false;
            }
        }
        public void Open()
        {
            isOpen = true;
            shouldOpenModal = true;
        }
    }
}
