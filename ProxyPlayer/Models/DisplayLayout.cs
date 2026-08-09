using System;

namespace ProxyPlayer.Models
{
    public enum DisplayLayout
    {
        Compact,
        Portrait,
    }
    public static class DisplayLayoutExtensions
    {
        public static string ToFriendlyString(this DisplayLayout layout)
        {
            return layout switch
            {
                DisplayLayout.Compact => "Compact",
                DisplayLayout.Portrait => "Portrait",
                _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null)
            };
        }
    }
}
