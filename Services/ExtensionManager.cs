using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HabboGPTer.Config;
using Xabbo.GEarth;

namespace HabboGPTer.Services;

public class BotInstance
{
    public int Port { get; set; }
    public HabboGPTerExtension Extension { get; set; } = null!;
    public Task? ConnectionTask { get; set; }
    public string Status { get; set; } = "Disconnected";
    public string Username { get; set; } = "";
    public bool IsConnected { get; set; }
}

public class ExtensionManager : IDisposable
{
    private readonly Dictionary<int, BotInstance> _instances = new();
    private readonly GEarthScanner _scanner;
    private readonly object _lock = new();

    public AISettings SharedAISettings { get; }

    public GEarthScanner Scanner => _scanner;

    public event Action<BotInstance>? OnInstanceAdded;

    public event Action<int>? OnInstanceRemoved;

    public event Action<BotInstance>? OnInstanceStateChanged;

    public event Action<List<GEarthScanResult>>? OnScanComplete;

    public ExtensionManager(int minPort = 9092, int maxPort = 9099)
    {
        SharedAISettings = AISettings.Load();

        _scanner = new GEarthScanner(minPort, maxPort);
        _scanner.OnScanComplete += OnScanCompleteInternal;
    }

    public IReadOnlyList<BotInstance> GetInstances()
    {
        lock (_lock)
        {
            return _instances.Values.ToList();
        }
    }

    public HashSet<int> GetConnectedPorts()
    {
        lock (_lock)
        {
            return _instances.Keys.ToHashSet();
        }
    }

    public bool IsPortConnected(int port)
    {
        lock (_lock)
        {
            return _instances.ContainsKey(port);
        }
    }

    public Task<BotInstance?> AddInstanceAsync(int port)
    {
        lock (_lock)
        {
            if (_instances.ContainsKey(port))
            {
                return Task.FromResult<BotInstance?>(_instances[port]);
            }
        }

        var extension = new HabboGPTerExtension(SharedAISettings);

        var instance = new BotInstance
        {
            Port = port,
            Extension = extension,
            Status = "Connecting..."
        };

        extension.Connected += args =>
        {
            instance.Status = "Connected";
            instance.IsConnected = true;
            OnInstanceStateChanged?.Invoke(instance);
        };

        extension.Disconnected += () =>
        {
            instance.Status = "Disconnected";
            instance.IsConnected = false;
            OnInstanceStateChanged?.Invoke(instance);
        };

        extension.OnUsernameDetected += username =>
        {
            instance.Username = username;
            OnInstanceStateChanged?.Invoke(instance);
        };

        lock (_lock)
        {
            _instances[port] = instance;
            _scanner.ConnectedPorts = GetConnectedPorts();
        }

        OnInstanceAdded?.Invoke(instance);

        instance.ConnectionTask = Task.Run(async () =>
        {
            try
            {
                await extension.RunAsync(new GEarthConnectOptions(Port: port));
            }
            catch (Exception ex)
            {
                instance.Status = $"Error: {ex.Message}";
                instance.IsConnected = false;
                OnInstanceStateChanged?.Invoke(instance);
            }
        });

        return Task.FromResult<BotInstance?>(instance);
    }

    public void RemoveInstance(int port)
    {
        BotInstance? instance = null;

        lock (_lock)
        {
            if (_instances.TryGetValue(port, out instance))
            {
                _instances.Remove(port);
                _scanner.ConnectedPorts = GetConnectedPorts();
            }
        }

        if (instance != null)
        {
            instance.Extension.Stop();
            OnInstanceRemoved?.Invoke(port);
        }
    }

    public async Task<List<GEarthScanResult>> ScanNowAsync()
    {
        return await _scanner.ScanAsync();
    }

    public void StartAutoDetection(int intervalMs = 10000, bool autoConnect = true)
    {
        _scanner.ConnectedPorts = GetConnectedPorts();
        _scanner.StartPeriodicScan(intervalMs);
    }

    public void StopAutoDetection()
    {
        _scanner.StopPeriodicScan();
    }

    public async Task AutoConnectNewInstancesAsync(List<GEarthScanResult> results)
    {
        foreach (var result in results)
        {
            if (result.IsAvailable && !result.IsConnected)
            {
                await AddInstanceAsync(result.Port);
            }
        }
    }

    private void OnScanCompleteInternal(List<GEarthScanResult> results)
    {
        var connectedPorts = GetConnectedPorts();
        foreach (var result in results)
        {
            result.IsConnected = connectedPorts.Contains(result.Port);
        }

        OnScanComplete?.Invoke(results);
    }

    public void Dispose()
    {
        _scanner.StopPeriodicScan();
        _scanner.Dispose();

        SharedAISettings.Save();

        lock (_lock)
        {
            foreach (var instance in _instances.Values)
            {
                instance.Extension.Stop();
            }
            _instances.Clear();
        }
    }
}
