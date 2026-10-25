using System;
using System.Net.Sockets;

namespace ProxyPlayer.Helpers
{
    internal static class AfUnixHelper
    {
        private static bool? AfUnixSupported;

        public static bool IsAfUnixSupported()
        {
            if (AfUnixSupported.HasValue) return AfUnixSupported.Value;

            try
            {
                using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                AfUnixSupported = true;
            }
            catch (SocketException)
            {
                Plugin.Log.Warning("AF_UNIX is not supported on this platform.");
                AfUnixSupported = false;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "AF_UNIX probe failed");
                AfUnixSupported = false;
            }

            return AfUnixSupported.Value;
        }
    }
}
