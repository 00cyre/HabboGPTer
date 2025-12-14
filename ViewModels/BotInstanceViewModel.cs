using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using HabboGPTer.Models;
using HabboGPTer.Services;

namespace HabboGPTer.ViewModels;

public class LogEntryViewModel
{
    public string Text { get; set; } = string.Empty;
    public IBrush Color { get; set; } = Brushes.White;
}

public class ChatMessageViewModel
{
    public string Text { get; set; } = string.Empty;
    public IBrush Color { get; set; } = Brushes.White;
    public DateTime Timestamp { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public bool IsOwnMessage { get; set; }
}

public class BotInstanceViewModel : ReactiveObject
{
    private readonly BotInstance _instance;

    public int Port => _instance.Port;

    private string _status = "Disconnected";
    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private string _username = "";
    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    private IBrush _statusColor = Brushes.Gray;
    public IBrush StatusColor
    {
        get => _statusColor;
        set => this.RaiseAndSetIfChanged(ref _statusColor, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _isEnabled, value);
            _instance.Extension.Enabled = value;
        }
    }

    public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();

    public ObservableCollection<ChatMessageViewModel> ChatMessages { get; } = new();

    public HabboGPTerExtension Extension => _instance.Extension;

    public string DisplayText => string.IsNullOrEmpty(Username)
        ? $"Port {Port}"
        : $"Port {Port} - {Username}";

    public BotInstanceViewModel(BotInstance instance)
    {
        _instance = instance;

        Status = instance.Status;
        Username = instance.Username;
        IsConnected = instance.IsConnected;
        UpdateStatusColor();

        // Sync IsEnabled from Extension (which defaults to false)
        _isEnabled = _instance.Extension.Enabled;

        _instance.Extension.OnUsernameDetected += OnUsernameDetected;
        _instance.Extension.Logger.OnLog += OnLogEntry;
        _instance.Extension.Logger.OnClear += OnLogClear;
        _instance.Extension.OnChatReceived += OnChatReceived;

        _instance.Extension.Connected += _ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Status = "Connected";
                IsConnected = true;
                StatusColor = Brushes.LimeGreen;
            });
        };

        _instance.Extension.Disconnected += () =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Status = "Disconnected";
                IsConnected = false;
                StatusColor = Brushes.Gray;
            });
        };
    }

    public void UpdateFromInstance()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Status = _instance.Status;
            Username = _instance.Username;
            IsConnected = _instance.IsConnected;
            UpdateStatusColor();
            this.RaisePropertyChanged(nameof(DisplayText));
        });
    }

    private void UpdateStatusColor()
    {
        StatusColor = IsConnected ? Brushes.LimeGreen : Brushes.Gray;
    }

    private void OnUsernameDetected(string username)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Username = username;
            this.RaisePropertyChanged(nameof(DisplayText));
        });
    }

    private void OnLogEntry(LogEntry entry)
    {
        var color = entry.Category switch
        {
            LogCategory.Error => Brushes.Red,
            LogCategory.Warning => Brushes.Orange,
            LogCategory.Chat => Brushes.Cyan,
            LogCategory.AI => Brushes.LimeGreen,
            LogCategory.Send => Brushes.Yellow,
            LogCategory.API => Brushes.Magenta,
            _ => Brushes.White
        };

        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(new LogEntryViewModel
            {
                Text = entry.ToString(),
                Color = color
            });

            while (LogEntries.Count > 500)
            {
                LogEntries.RemoveAt(0);
            }
        });
    }

    private void OnLogClear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Clear();
        });
    }

    private void OnChatReceived(ChatMessage message)
    {
        var color = message.IsShout ? Brushes.Orange :
                    message.IsWhisper ? Brushes.LightBlue : Brushes.White;

        Dispatcher.UIThread.Post(() =>
        {
            ChatMessages.Add(new ChatMessageViewModel
            {
                Text = $"{message.SenderName}: {message.Content}",
                Color = color,
                Timestamp = message.Timestamp,
                SenderName = message.SenderName,
                IsOwnMessage = false
            });

            while (ChatMessages.Count > 200)
            {
                ChatMessages.RemoveAt(0);
            }
        });
    }
}
