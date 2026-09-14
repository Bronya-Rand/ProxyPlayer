using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace ProxyPlayer.Media.MPRIS
{
    internal sealed class MprisWineConnectionOptions(string address) : DBusConnectionOptions
    {
        /// <summary>
        /// Paths to check for the machine-id file on Linux systems.
        /// </summary>
        private static readonly string[] MachineIdPaths =
        [
            "/var/lib/dbus/machine-id",
            "/etc/machine-id"
        ];

        /// <summary>
        /// Reads the machine ID from the standard locations on Linux systems.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The machine ID.</returns>
        /// <exception cref="FileNotFoundException"></exception>
        private static async ValueTask<string> ReadMachineIdAsync(CancellationToken cancellationToken)
        {
            foreach (var path in MachineIdPaths)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    var content = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
                    if (content.Length >= 32)
                        // Parse the hex chars into Guid
                        return Guid.Parse(content.AsSpan(0, 32)).ToString("N");
                }
                catch
                {
                    // Fall back to the next path if reading fails
                }
            }
            throw new FileNotFoundException("Unable to find a valid machine-id.");
        }

        /// <summary>
        /// Sets up the D-Bus connection options for a Wine environment.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The setup result.</returns>
        /// <exception cref="Exception"></exception>
        protected override async ValueTask<SetupResult> SetupAsync(CancellationToken cancellationToken)
        {
            // Get UID of XIV process to set the correct UID for the D-Bus connection
            using var reader = new StreamReader("/proc/self/status");
            string? line;
            string? uid = null;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (line.StartsWith("Uid:"))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        uid = parts[2]; // Get the effective UID
                        break;
                    }
                }
            }

            // Get the machine ID for the D-Bus connection
            var machineId = await ReadMachineIdAsync(cancellationToken).ConfigureAwait(false);

            if (uid != null)
            {
                return new SetupResult(address)
                {
                    SupportsFdPassing = false,
                    UserId = uid,
                    MachineId = machineId
                };
            }
            throw new Exception("Could not determine the effective UID for the D-Bus connection.");
        }
    }
}
