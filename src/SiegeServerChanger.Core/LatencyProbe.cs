using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SiegeServerChanger.Core;

/// <summary>
/// Estimates latency to each region by timing TCP connects to an AWS endpoint in that region.
/// ICMP is often blocked, and the GameLift API endpoint does not exist in every region,
/// so we use DynamoDB, which does. This is a relative indicator, not the in-game ping.
/// </summary>
public static class LatencyProbe
{
    public const int Port = 443;

    /// <summary>Endpoint to probe, or null when we have no verified endpoint (e.g. playfab/Azure).</summary>
    public static string? GetHost(DataCenter dc)
    {
        if (!dc.Id.StartsWith("gamelift/", StringComparison.OrdinalIgnoreCase)) return null;
        var region = dc.Region;
        if (region.Length == 0 || !region.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) return null;
        return $"dynamodb.{region.ToLowerInvariant()}.amazonaws.com";
    }

    /// <summary>Median of the successful samples, or null if none succeeded.</summary>
    public static double? Median(IEnumerable<double> samples)
    {
        var s = samples.Order().ToArray();
        if (s.Length == 0) return null;
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    }

    /// <summary>Median connect time in ms (DNS lookup excluded), or null if unreachable.</summary>
    public static async Task<double?> MeasureAsync(string host, int attempts = 4, int timeoutMs = 2000,
        int port = Port, CancellationToken ct = default)
    {
        IPAddress? ip;
        try
        {
            var addrs = await Dns.GetHostAddressesAsync(host, ct);
            ip = addrs.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addrs.FirstOrDefault();
        }
        catch (SocketException) { return null; }
        if (ip is null) return null;

        var samples = new List<double>();
        for (int i = 0; i < attempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeoutMs);
            var sw = Stopwatch.StartNew();
            try
            {
                await socket.ConnectAsync(new IPEndPoint(ip, port), timeout.Token);
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* timed out */ }
            catch (SocketException) { /* refused / unreachable */ }
        }
        return Median(samples);
    }

    /// <summary>
    /// Measures every data center with limited parallelism. onResult is called as each finishes
    /// (value null = unreachable or not measurable).
    /// </summary>
    public static async Task MeasureAllAsync(IEnumerable<DataCenter> centers, Action<DataCenter, double?> onResult,
        int parallelism = 4, CancellationToken ct = default)
    {
        using var gate = new SemaphoreSlim(parallelism);
        var tasks = centers.Select(async dc =>
        {
            var host = GetHost(dc);
            if (host is null) { onResult(dc, null); return; }
            await gate.WaitAsync(ct);
            try { onResult(dc, await MeasureAsync(host, ct: ct)); }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
    }
}
