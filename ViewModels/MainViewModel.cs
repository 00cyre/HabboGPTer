using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using HabboGPTer.Config;
using HabboGPTer.Services;

namespace HabboGPTer.ViewModels;

public class MainViewModel : ReactiveObject, IDisposable
{
    private readonly ExtensionManager _extensionManager;
    private readonly DispatcherTimer _refreshTimer;

    public ObservableCollection<BotInstanceViewModel> Instances { get; } = new();

    public ObservableCollection<GEarthScanResult> AvailablePorts { get; } = new();

    private BotInstanceViewModel? _selectedInstance;
    public BotInstanceViewModel? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            if (_selectedInstance != value)
            {
                if (_selectedInstance != null)
                    _selectedInstance.IsSelected = false;

                this.RaiseAndSetIfChanged(ref _selectedInstance, value);

                if (_selectedInstance != null)
                    _selectedInstance.IsSelected = true;

                this.RaisePropertyChanged(nameof(CurrentStatusText));
                this.RaisePropertyChanged(nameof(CurrentUsername));
                this.RaisePropertyChanged(nameof(CurrentLogEntries));
                this.RaisePropertyChanged(nameof(CurrentChatMessages));
                this.RaisePropertyChanged(nameof(HasSelectedInstance));
            }
        }
    }

    public bool HasSelectedInstance => SelectedInstance != null;

    private bool _autoDetectEnabled = true;
    public bool AutoDetectEnabled
    {
        get => _autoDetectEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _autoDetectEnabled, value);
            if (value)
                _extensionManager.StartAutoDetection(10000, autoConnect: true);
            else
                _extensionManager.StopAutoDetection();
        }
    }

    private bool _autoConnectEnabled = true;
    public bool AutoConnectEnabled
    {
        get => _autoConnectEnabled;
        set => this.RaiseAndSetIfChanged(ref _autoConnectEnabled, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }

    public string CurrentStatusText => SelectedInstance?.Status ?? "No instance selected";
    public string CurrentUsername => SelectedInstance?.Username ?? "";
    public ObservableCollection<LogEntryViewModel>? CurrentLogEntries => SelectedInstance?.LogEntries;
    public ObservableCollection<ChatMessageViewModel>? CurrentChatMessages => SelectedInstance?.ChatMessages;

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set
        {
            this.RaiseAndSetIfChanged(ref _apiKey, value);
            _extensionManager.SharedAISettings.SetApiKey(value);
        }
    }

    private string _model = "openai/gpt-oss-120b:free";
    public string Model
    {
        get => _model;
        set
        {
            this.RaiseAndSetIfChanged(ref _model, value);
            _extensionManager.SharedAISettings.SetModel(value);
        }
    }

    private string _characterName = "Visitante";
    public string CharacterName
    {
        get => _characterName;
        set
        {
            this.RaiseAndSetIfChanged(ref _characterName, value);
            _extensionManager.SharedAISettings.SetCharacter(value, CharacterPersonality);
        }
    }

    private string _characterPersonality = "Uma pessoa amigavel e descontraida que gosta de conversar e fazer amigos no Habbo. Usa girias brasileiras e e bem humorado.";
    public string CharacterPersonality
    {
        get => _characterPersonality;
        set
        {
            this.RaiseAndSetIfChanged(ref _characterPersonality, value);
            _extensionManager.SharedAISettings.SetCharacter(CharacterName, value);
        }
    }

    private int _minResponseDelay = 5;
    public int MinResponseDelay
    {
        get => _minResponseDelay;
        set
        {
            this.RaiseAndSetIfChanged(ref _minResponseDelay, value);
            _extensionManager.SharedAISettings.SetResponseDelay(value, MaxResponseDelay);
        }
    }

    private int _maxResponseDelay = 7;
    public int MaxResponseDelay
    {
        get => _maxResponseDelay;
        set
        {
            this.RaiseAndSetIfChanged(ref _maxResponseDelay, value);
            _extensionManager.SharedAISettings.SetResponseDelay(MinResponseDelay, value);
        }
    }

    private bool _aiEnabled = true;
    public bool AIEnabled
    {
        get => _aiEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _aiEnabled, value);
            _extensionManager.SharedAISettings.SetEnabled(value);
        }
    }

    private int _instanceCount;
    public int InstanceCount
    {
        get => _instanceCount;
        set => this.RaiseAndSetIfChanged(ref _instanceCount, value);
    }

    private int _connectedCount;
    public int ConnectedCount
    {
        get => _connectedCount;
        set => this.RaiseAndSetIfChanged(ref _connectedCount, value);
    }

    public ReactiveCommand<Unit, Unit> ScanNowCommand { get; }
    public ReactiveCommand<int, Unit> ConnectToPortCommand { get; }
    public ReactiveCommand<int, Unit> DisconnectPortCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearLogCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearChatCommand { get; }
    public ReactiveCommand<string, Unit> SendChatCommand { get; }

    public MainViewModel()
    {
        _extensionManager = new ExtensionManager();

        _extensionManager.OnInstanceAdded += OnInstanceAdded;
        _extensionManager.OnInstanceRemoved += OnInstanceRemoved;
        _extensionManager.OnInstanceStateChanged += OnInstanceStateChanged;
        _extensionManager.OnScanComplete += OnScanComplete;

        ScanNowCommand = ReactiveCommand.CreateFromTask(ScanNowAsync);
        ConnectToPortCommand = ReactiveCommand.CreateFromTask<int>(ConnectToPortAsync);
        DisconnectPortCommand = ReactiveCommand.Create<int>(DisconnectPort);
        ClearLogCommand = ReactiveCommand.Create(ClearLog);
        ClearChatCommand = ReactiveCommand.Create(ClearChat);
        SendChatCommand = ReactiveCommand.Create<string>(SendChat);

        var settings = _extensionManager.SharedAISettings;
        _apiKey = settings.ApiKey;
        _model = settings.Model;
        _characterName = settings.CharacterName;
        _characterPersonality = settings.CharacterPersonality;
        _minResponseDelay = settings.MinResponseDelaySec;
        _maxResponseDelay = settings.MaxResponseDelaySec;
        _aiEnabled = settings.IsEnabled;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (s, e) => RefreshInstanceStats();
        _refreshTimer.Start();

        if (_autoDetectEnabled)
        {
            _extensionManager.StartAutoDetection(10000);
        }

        _ = ScanNowAsync();
    }

    private void OnInstanceAdded(BotInstance instance)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new BotInstanceViewModel(instance);
            Instances.Add(vm);

            if (SelectedInstance == null)
            {
                SelectedInstance = vm;
            }

            UpdateInstanceCounts();
        });
    }

    private void OnInstanceRemoved(int port)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = Instances.FirstOrDefault(i => i.Port == port);
            if (vm != null)
            {
                Instances.Remove(vm);
                if (SelectedInstance == vm)
                {
                    SelectedInstance = Instances.FirstOrDefault();
                }
            }

            UpdateInstanceCounts();
        });
    }

    private void OnInstanceStateChanged(BotInstance instance)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = Instances.FirstOrDefault(i => i.Port == instance.Port);
            vm?.UpdateFromInstance();

            if (vm == SelectedInstance)
            {
                this.RaisePropertyChanged(nameof(CurrentStatusText));
                this.RaisePropertyChanged(nameof(CurrentUsername));
            }

            UpdateInstanceCounts();
        });
    }

    private void OnScanComplete(List<GEarthScanResult> results)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsScanning = false;

            AvailablePorts.Clear();
            foreach (var result in results.Where(r => r.IsAvailable && !r.IsConnected))
            {
                AvailablePorts.Add(result);
            }

            if (AutoConnectEnabled)
            {
                _ = _extensionManager.AutoConnectNewInstancesAsync(results);
            }
        });
    }

    private async Task ScanNowAsync()
    {
        IsScanning = true;
        await _extensionManager.ScanNowAsync();
    }

    private async Task ConnectToPortAsync(int port)
    {
        await _extensionManager.AddInstanceAsync(port);
    }

    private void DisconnectPort(int port)
    {
        _extensionManager.RemoveInstance(port);
    }

    private void UpdateInstanceCounts()
    {
        InstanceCount = Instances.Count;
        ConnectedCount = Instances.Count(i => i.IsConnected);
    }

    private void RefreshInstanceStats()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConnectedCount = Instances.Count(i => i.IsConnected);
        });
    }

    private void ClearLog()
    {
        SelectedInstance?.Extension.Logger.Clear();
    }

    private void ClearChat()
    {
        Dispatcher.UIThread.Post(() =>
        {
            SelectedInstance?.ChatMessages.Clear();
            SelectedInstance?.Extension.ConversationContext.Clear();
        });
    }

    private void SendChat(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || SelectedInstance == null)
            return;

        SelectedInstance.Extension.SendChat(message);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _extensionManager.Dispose();
    }
}
