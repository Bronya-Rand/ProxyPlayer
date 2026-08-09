/*
 * ProxyPlayerServer - Handles connecting to Windows Media Controls from within XIV.
 * 
 * Do not run this process directly nor run this as admin. It must run as a user.
 * Attempting to run this as admin while XIV is not (why would you run the game as admin?)
 * will cause the connection to fail with UnauthorizedAccessException.
 */
using ProxyPlayerServer;

const string MutexName = "Local\\ProxyPlayerServer.SingleInstance";
using var singleInstanceMutex = new Mutex(false, MutexName, out var _);

bool acquired;
try
{
    acquired = singleInstanceMutex.WaitOne(0);

}
catch (AbandonedMutexException)
{
    // Crash occurred in another instance - reacquire
    acquired = true;
}

if (!acquired)
{
    Console.WriteLine("Another instance of MediaProxy is already running. Exiting.");
    return;
}

try
{
    var mediaService = new MediaSessionService();
    await mediaService.InitializeAsync();

    var server = new PipeServer(mediaService);
    server.Start();

    Console.WriteLine("MediaProxy server is running. Press Ctrl+C to exit.");
    await Task.Delay(Timeout.Infinite);
}
finally
{
    singleInstanceMutex.ReleaseMutex();
}
