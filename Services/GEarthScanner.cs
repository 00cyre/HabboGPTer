using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HabboGPTer.Services;

public class GEarthScanResult
{
    public int Port { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsConnected { get; set; }
}

public class GEarthScanner : IDisposable
{
    private readonly int _minPort;
    private readonly int _maxPort;
    private readonly int _connectionTimeoutMs;
    private readonly int _packetTimeoutMs;
    private Timer? _scanTimer;
    private bool _isScanning;
    private readonly object _lock = new();

    public event Action<List<GEarthScanResult>>? OnScanComplete;

    public HashSet<int> ConnectedPorts { get; set; } = new();

    public GEarthScanner(int minPort = 9092, int maxPort = 9099,
                          int connectionTimeoutMs = 500, int packetTimeoutMs = 1500)
    {
        _minPort = minPort;
        _maxPort = maxPort;
        _connectionTimeoutMs = connectionTimeoutMs;
        _packetTimeoutMs = packetTimeoutMs;
    }

    public async Task<List<GEarthScanResult>> ScanAsync()
    {
        lock (_lock)
        {
            if (_isScanning) return new List<GEarthScanResult>();
            _isScanning = true;
        }

        try
        {
            var tasks = new List<Task<GEarthScanResult>>();

            for (int port = _minPort; port <= _maxPort; port++)
            {
                int p = port;
                bool isConnected = ConnectedPorts.Contains(p);
                tasks.Add(ProbePortAsync(p, isConnected));
            }

            var results = await Task.WhenAll(tasks);
            var availableResults = results.Where(r => r.IsAvailable).ToList();

            OnScanComplete?.Invoke(availableResults);
            return availableResults;
        }
        finally
        {
            lock (_lock) { _isScanning = false; }
        }
    }

    private async Task<GEarthScanResult> ProbePortAsync(int port, bool isAlreadyConnected)
    {
        if (isAlreadyConnected)
        {
            return new GEarthScanResult { Port = port, IsAvailable = true, IsConnected = true };
        }

        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(_connectionTimeoutMs);

            await client.ConnectAsync("127.0.0.1", port, cts.Token);

            using var cts2 = new CancellationTokenSource(_packetTimeoutMs);
            var stream = client.GetStream();

            var buffer = new byte[6];
            int totalRead = 0;

            while (totalRead < 6)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, 6 - totalRead), cts2.Token);
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }

            if (totalRead >= 6)
            {
                short header = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(4, 2));

                if (header == 2)
                {
                    return new GEarthScanResult { Port = port, IsAvailable = true, IsConnected = false };
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        catch { }

        return new GEarthScanResult { Port = port, IsAvailable = false, IsConnected = false };
    }

    public void StartPeriodicScan(int intervalMs = 10000)
    {
        StopPeriodicScan();

        _scanTimer = new Timer(async _ =>
        {
            await ScanAsync();
        }, null, 0, intervalMs);
    }

    public void StopPeriodicScan()
    {
        _scanTimer?.Dispose();
        _scanTimer = null;
    }

    public void Dispose()
    {
        StopPeriodicScan();
    }
}
