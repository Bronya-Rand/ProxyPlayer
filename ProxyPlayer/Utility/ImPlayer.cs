using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;

namespace ProxyPlayer.Utility
{
    public static class ImPlayer
    {
        /// <summary>
        /// Renders a clickable FontAwesome icon with hover color change and tooltip.
        /// Returns true if the icon was clicked.
        /// </summary>
        /// <param name="id">ImGui ID for the icon (must be unique, use ### prefix)</param>
        /// <param name="icon">FontAwesome icon to display</param>
        /// <param name="tooltip">Tooltip text to show on hover</param>
        /// <param name="activeColor">Color when not hovered (default: white)</param>
        /// <param name="hoverColor">Color when hovered (default: Dalamud yellow)</param>
        /// <returns>True if the icon was clicked</returns>
        public static bool DrawClickableIcon(
            string id,
            FontAwesomeIcon icon,
            string? tooltip = null,
            Vector4? activeColor = null,
            Vector4? hoverColor = null)
        {
            var defaultColor = activeColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            var highlightColor = hoverColor ?? ImGuiColors.DalamudYellow;

            string iconText;
            Vector2 textSize;
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                iconText = icon.ToIconString();
                textSize = ImGui.CalcTextSize(iconText);
            }

            // Use an InvisibleButton as the actual hit-target so ImGui properly
            // registers it as an interactive item (IsItemClicked, IsItemHovered, etc.).
            var cursorPos = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton(id, textSize);
            var isClicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
            var isHovered = ImGui.IsItemHovered();

            // Draw the icon text manually on top of the invisible button area
            var color = isHovered ? highlightColor : defaultColor;
            var colorUint = ImGui.ColorConvertFloat4ToU32(color);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.GetWindowDrawList().AddText(cursorPos, colorUint, iconText);
            }

            if (tooltip != null && isHovered)
            {
                using (ImRaii.PushFont(UiBuilder.DefaultFont))
                    ImGui.SetTooltip(tooltip);
            }

            return isClicked;
        }

        /// <summary>
        /// Renders scrolling text if it exceeds the available width. Otherwise, renders normally.
        /// </summary>
        /// <param name="id">The ID of the text element</param>
        /// <param name="text">The text to display</param>
        /// <param name="availWidth">The available width for the text</param>
        /// <param name="color">The color the text should be displayed in</param>
        /// <param name="scrollSpeed">How fast the text should scroll</param>
        /// <param name="pauseDuration">The duration to pause at the start and end of the scroll</param>
        public static void TextScrolling(
            string id,
            string text,
            float availWidth,
            Vector4? color = null,
            float scrollSpeed = 35f,
            float pauseDuration = 1.5f)
        {
            var textSize = ImGui.CalcTextSize(text);

            if (textSize.X <= availWidth)
            {
                if (color.HasValue)
                    ImGui.TextColored(color.Value, text);
                else
                    ImGui.Text(text);
                return;
            }

            // Calculate the scrolling offset based on time
            var overflowWidth = textSize.X - availWidth + 8f; // Add some padding
            var scrollDuration = overflowWidth / scrollSpeed;
            var totalCycleDuration = (scrollDuration + pauseDuration) * 2f; // Scroll forward and backward
            var time = ImGui.GetTime() % totalCycleDuration;

            float offset;
            if (time < pauseDuration)
            {
                offset = 0f;
            }
            else if (time < pauseDuration + scrollDuration)
            {
                // Scroll forward
                var progress = (time - pauseDuration) / scrollDuration;
                offset = (float)progress * overflowWidth;
            }
            else if (time < (pauseDuration * 2f) + scrollDuration)
            {
                // Pause at the end
                offset = overflowWidth;

            }
            else
            {
                // Scroll backward
                var progress = (time - ((pauseDuration * 2f) + scrollDuration)) / scrollDuration;
                offset = (1f - (float)progress) * overflowWidth;
            }

            // Render text
            using var child = ImRaii.Child($"scroll_{id}", new Vector2(availWidth, textSize.Y), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            {
                if (child)
                {
                    ImGui.SetCursorPosX(-offset);
                    if (color.HasValue)
                        ImGui.TextColored(color.Value, text);
                    else
                        ImGui.Text(text);
                }
            }
        }
    }
}
