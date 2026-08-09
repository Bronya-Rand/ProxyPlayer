using System;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace ProxyPlayer.Media
{
    /// <summary>
    /// Manages the Server Info Bar entry in XIV's DTR bar
    /// </summary>
    public sealed class DTRDisplay : IDisposable
    {
        private readonly IDtrBarEntry? dtrEntry;
        private readonly Plugin plugin;
        private readonly PipeClient pipeClient;

        public DTRDisplay(Plugin plugin, PipeClient pipeClient)
        {
            this.plugin = plugin;
            this.pipeClient = pipeClient;

            if (Plugin.DtrBar.Get("ProxyPlayer") is { } entry)
            {
                this.dtrEntry = entry;

                dtrEntry.Text = "ProxyPlayer";
                dtrEntry.Shown = false;
                dtrEntry.OnClick += OnClick();
            }
        }

        public void Dispose()
        {
            if (dtrEntry == null) return;

            dtrEntry.OnClick -= OnClick();
            dtrEntry.Remove();
        }

        public void UpdateDtr(IFramework framework)
        {
            UpdateVisibility(true);
            UpdateBarString();
        }
        private void UpdateBarString()
        {
            if (dtrEntry == null) return;

            // Update the DTR bar string based on the current state of the pipe client
            // Obviously get only the first 9 characters of the song name to avoid overflow
            var seIcon = SeIconChar.AutoTranslateClose.ToIconString();
            var tooltip = "ProxyPlayer server not running";
            var dtrText = "Not Connected";
            if (pipeClient.IsConnected)
            {
                if (pipeClient.CurrentState.SelectedAppId == null)
                {
                    dtrText = "No Session";
                    tooltip = "No media session selected";
                }
                else
                {
                    var songName = pipeClient.CurrentState.Title.Length > Constants.MaxTitleLengthDtr
                        ? pipeClient.CurrentState.Title[..(Constants.MaxTitleLengthDtr - 3)] + "..."
                        : pipeClient.CurrentState.Title;
                    dtrText = songName;
                    tooltip = pipeClient.CurrentState.Title;
                }
            }
            dtrEntry.Text = $"{seIcon} {dtrText}";
            dtrEntry.Tooltip = tooltip;
        }
        private void UpdateVisibility(bool isVisible) => dtrEntry?.Shown = isVisible;

        // Open MainWindow when the DTR entry is clicked
        private Action<DtrInteractionEvent>? OnClick()
        {
            return _ =>
            {
                plugin.ToggleMainUi();
            };
        }
    }
}
