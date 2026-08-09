using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using ProxyPlayer;

namespace ProxyPlayer.Media
{
    /// <summary>
    /// Manages the lifecycle of the ProxyPlayer process.
    /// </summary>
    public sealed class ProxyProcessManager : IDisposable
    {
        private static FileInfo ProxyServerPath => new(Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory!.FullName, "Resources/bin", "ProxyPlayerServer.exe"));
        private Process? process;

        public ProxyProcessManager()
        {
            if (Util.IsWine())
            {
                Plugin.NotificationManager.AddNotification(new Notification
                {
                    Content = "This plugin is meant to be run on Windows. Linux/Wine support has not been made.",
                    Type = NotificationType.Error
                });
                return;
            }
            _ = StartProxyServer();
        }
        private async Task StartProxyServer()
        {
            if (await IsProxyServerReachableAsync())
            {
                Plugin.Log.Info("ProxyPlayerServer is already running");
                return;
            }

            try
            {
                var proxyServer = new ProcessStartInfo
                {
                    FileName = ProxyServerPath.FullName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                process = Process.Start(proxyServer);
                Plugin.Log.Info($"Started ProxyPlayerServer with PID {process?.Id}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to start ProxyPlayerServer");
            }
        }
        private static async Task<bool> IsProxyServerReachableAsync()
        {
            try
            {
                using var probe = new NamedPipeClientStream(".", "ProxyPlayerStatePipe", PipeDirection.In);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
                await probe.ConnectAsync(cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public void Dispose()
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                    Plugin.Log.Info($"Stopped ProxyPlayerServer with PID {process.Id}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Failed to stop ProxyPlayerServer");
                }
            }
        }
    }
}
