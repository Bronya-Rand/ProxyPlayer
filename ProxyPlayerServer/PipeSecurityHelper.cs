using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ProxyPlayerServer
{
    internal static class PipeSecurityHelper
    {
        /// <summary>
        /// Creates a PipeSecurity to allow only the current user to access the named pipe.
        /// </summary>
        /// <remarks>
        /// Not committing a MSI Center here.
        /// </remarks>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static PipeSecurity CreateCurrentUserSecurity()
        {
            var security = new PipeSecurity();

            var currentUser = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("Unable to resolve current user SID.");

            security.AddAccessRule(new PipeAccessRule(
                currentUser,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));

            // Deny any network/remote connections to the pipe for security reasons
            var networkSid = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
            security.AddAccessRule(new PipeAccessRule(
                networkSid,
                PipeAccessRights.FullControl,
                AccessControlType.Deny));

            return security;
        }
    }
}
