using System.Net;
using System.Net.Sockets;
using SiegeServerChanger.Core;

namespace SiegeServerChanger.Tests;

public class LatencyProbeTests
{
    [Theory]
    [InlineData("gamelift/ap-northeast-2", "dynamodb.ap-northeast-2.amazonaws.com")]
    [InlineData("gamelift/US-EAST-1", "dynamodb.us-east-1.amazonaws.com")]
    [InlineData("default", null)]
    [InlineData("playfab/uaenorth", null)]
    [InlineData("gamelift/bad.host/x", null)]
    public void Maps_data_center_to_host(string id, string? expected)
    {
        Assert.Equal(expected, LatencyProbe.GetHost(DataCenterCatalog.Create(id)));
    }

    [Fact]
    public void Median_handles_odd_even_and_empty()
    {
        Assert.Equal(5, LatencyProbe.Median([9, 1, 5]));
        Assert.Equal(4, LatencyProbe.Median([2, 6, 1, 9]));
        Assert.Null(LatencyProbe.Median([]));
    }

    [Fact]
    public async Task Measures_local_listener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var ms = await LatencyProbe.MeasureAsync("127.0.0.1", attempts: 3, timeoutMs: 1000, port: port);
            Assert.NotNull(ms);
            Assert.InRange(ms!.Value, 0, 1000);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task Unknown_host_returns_null()
    {
        Assert.Null(await LatencyProbe.MeasureAsync("no-such-host.invalid", attempts: 1, timeoutMs: 500));
    }

    [Fact]
    public async Task Closed_port_returns_null()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Assert.Null(await LatencyProbe.MeasureAsync("127.0.0.1", attempts: 1, timeoutMs: 500, port: port));
    }

    [Fact]
    public async Task MeasureAll_reports_unmeasurable_as_null_without_network()
    {
        var results = new List<(string, double?)>();
        await LatencyProbe.MeasureAllAsync(
            [DataCenterCatalog.Create("default"), DataCenterCatalog.Create("playfab/uaenorth")],
            (dc, ms) => { lock (results) results.Add((dc.Id, ms)); });
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Null(r.Item2));
    }
}
