using System.Net;
using System.Net.Sockets;

namespace Api2Skill.Tests;

/// <summary>
/// Binds an <see cref="HttpListener"/> to a free loopback port, retrying on bind races.
///
/// <see cref="HttpListener"/> has no atomic "bind to any free port" the way
/// <see cref="TcpListener"/>/<see cref="Socket"/> do — the usual workaround (bind a throwaway
/// socket to port 0 to ask the OS for a free port, close it, then bind the real listener to
/// that port number) has an inherent time-of-check/time-of-use race: between closing the probe
/// socket and binding the listener, another test class grabbing a port the same way can win
/// it first. xUnit parallelizes across test classes by default, so with three different test
/// classes each starting an HttpListener in their fixture setup, this race is real, not
/// theoretical — it surfaced as genuine, if infrequent
/// (<c>System.Net.HttpListenerException: Address already in use</c>) CI flakiness, not
/// something anticipated up front.
/// </summary>
internal static class LoopbackHttpListenerFactory
{
    public static (HttpListener Listener, int Port) Start(int maxAttempts = 5)
    {
        HttpListenerException? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var port = GetCandidatePort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (HttpListenerException ex)
            {
                lastError = ex;
                listener.Close();
            }
        }
        throw new InvalidOperationException(
            $"Could not bind a loopback HttpListener after {maxAttempts} attempts.", lastError);
    }

    private static int GetCandidatePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
