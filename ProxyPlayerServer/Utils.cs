using System.Diagnostics;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace ProxyPlayerServer
{
    public static class Utils
    {
        /// <summary>
        /// Gets the friendly display name for a given app ID.
        /// </summary>
        /// <param name="appId">The app ID to resolve</param>
        /// <returns></returns>
        public static string ResolveFriendlyName(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return appId;

            // Case 1: Absolute file path
            if (Path.IsPathRooted(appId) && File.Exists(appId))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(appId);
                    var name = versionInfo.FileDescription ?? versionInfo.ProductName;
                    if (!string.IsNullOrWhiteSpace(name))
                        return name.Trim();
                }
                catch { }
            }

            // Case 2: Package Family Name (UWP)
            if (appId.Contains('!'))
            {
                var parts = appId.Split('!');
                var packageFamilyName = parts[0];
                try
                {
                    GetUWPPackage(packageFamilyName, out var package);
                    if (package != null)
                    {
                        var entry = package.GetAppListEntries()?[0];
                        if (entry?.DisplayInfo?.DisplayName is string displayName && !string.IsNullOrWhiteSpace(displayName))
                            return displayName;

                        return package.Id.Name;
                    }
                }
                catch { }

                // Fallback to parsing the package family name
                var packageParts = packageFamilyName.Split('.');
                if (packageParts.Length > 1)
                {
                    var secondPart = packageParts[1];
                    var underscoreIndex = secondPart.IndexOf('_');
                    if (underscoreIndex > 0)
                        secondPart = secondPart[..underscoreIndex];

                    return secondPart;
                }
                return parts[^1];
            }

            // Case 3: Registry lookup for custom AppUserModelId (e.g. Firefox)
            try
            {
                using var key = GetAppUserModelRegKey(appId);
                if (key?.GetValue("DisplayName") is string regDisplayName && !string.IsNullOrWhiteSpace(regDisplayName))
                {
                    // DisplayName may be an indirect string reference like "@{...}"
                    if (!regDisplayName.StartsWith('@'))
                        return regDisplayName.Trim();
                }
            }
            catch { }

            // Case 4: Win32 filename or App ID
            var exeName = Path.GetFileNameWithoutExtension(appId);
            if (!string.IsNullOrEmpty(exeName))
            {
                if (char.IsLower(exeName[0]))
                    exeName = char.ToUpper(exeName[0]) + exeName[1..];

                return exeName;
            }

            return appId;
        }

        private static void GetUWPPackage(string packageFamilyName, out Package? package)
        {
            var packageManager = new PackageManager();
            package = packageManager.FindPackages(packageFamilyName).FirstOrDefault();
        }

        private static RegistryKey? GetAppUserModelRegKey(string appId)
        {
            using var aumidRoot = Registry.CurrentUser.OpenSubKey(@"Software\Classes\AppUserModelId");
            if (aumidRoot != null)
            {
                // Try exact match first, then suffix match (e.g. "FirefoxToast-308046B0AF4A39CB")
                var matchingKeyName = aumidRoot.GetSubKeyNames()
                    .FirstOrDefault(name => name.Equals(appId, StringComparison.OrdinalIgnoreCase)
                                         || name.EndsWith(appId, StringComparison.OrdinalIgnoreCase));

                if (matchingKeyName != null)
                    return aumidRoot.OpenSubKey(matchingKeyName);
            }
            return null;
        }
    }
}
