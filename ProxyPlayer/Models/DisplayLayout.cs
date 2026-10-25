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
        extension(DisplayLayout layout)
        {
            public string ToFriendlyString() =>
                layout switch
                {
                    DisplayLayout.Compact => "Compact",
                    DisplayLayout.Portrait => "Portrait",
                    _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null)
                };
        }
    }
}
