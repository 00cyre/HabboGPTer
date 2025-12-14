using System;
using System.Threading;
using System.Threading.Tasks;
using HabboGPTer.Config;
using HabboGPTer.Models;
using HabboGPTer.Services;
using Xabbo;
using Xabbo.GEarth;
using Xabbo.Messages;
using Xabbo.Core;
using Xabbo.Core.Game;
using Xabbo.Core.Events;

namespace HabboGPTer;

[Extension(Name = "HabboGPTer", Author = "HabboGPTer", Version = "1.0.0")]
public partial class HabboGPTerExtension : GEarthExtension
{
    private static readonly Identifier InChat = new(ClientType.None, Direction.In, "Chat");
    private static readonly Identifier InShout = new(ClientType.None, Direction.In, "Shout");
    private static readonly Identifier InWhisper = new(ClientType.None, Direction.In, "Whisper");
    private static readonly Identifier OutChat = new(ClientType.None, Direction.Out, "Chat");
    private static readonly Identifier OutShout = new(ClientType.None, Direction.Out, "Shout");

    private readonly AISettings _aiSettings;
    private readonly Logger _logger;
    private readonly OpenRouterService _openRouter;
    private readonly ConversationContext _conversationContext;
    private readonly Random _random = new();

    private readonly RoomManager _roomManager;
    private readonly ProfileManager _profileManager;

    private Timer? _responseTimer;
    private readonly object _timerLock = new();
    private bool _pendingResponse;
    private CancellationTokenSource? _responseCts;
    private int _resetCount;
    private const int MaxResets = 3;

    public Logger Logger => _logger;

    public ConversationContext ConversationContext => _conversationContext;

    public bool Enabled { get; set; } = false; // Disabled by default - user enables manually

    public event Action<string>? OnUsernameDetected;

    public event Action<ChatMessage>? OnChatReceived;

    public HabboGPTerExtension(AISettings aiSettings)
    {
        _aiSettings = aiSettings;
        _logger = new Logger(enableFileLogging: true);
        _openRouter = new OpenRouterService(_logger, _aiSettings);
        _conversationContext = new ConversationContext();

        _logger.Info("Extension created, waiting for connection...");

        Connected += OnExtensionConnected;
        Disconnected += OnExtensionDisconnected;

        _roomManager = new RoomManager(this);
        _profileManager = new ProfileManager(this);

        _profileManager.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ProfileManager.UserData) && _profileManager.UserData != null)
            {
                var username = _profileManager.UserData.Name;
                _logger.Info($"Username detected: {username}");
                OnUsernameDetected?.Invoke(username);
            }
        };

        Intercepted += OnAnyPacket;
    }

    private void OnExtensionConnected(ConnectedEventArgs e)
    {
        _logger.Info($"Connected to G-Earth on {e.Host}:{e.Port}");
        _logger.Info($"Session: {e.Session.Hotel} - {e.Session.Client.Type} ({e.Session.Client.Version})");
    }

    private void OnExtensionDisconnected()
    {
        _logger.Warning("Disconnected from G-Earth");
        CancelPendingResponse();
    }

    private void OnAnyPacket(Intercept e)
    {
        try
        {
            if (e.Is(InChat))
            {
                ProcessChatPacket(e, isShout: false, isWhisper: false);
            }
            else if (e.Is(InShout))
            {
                ProcessChatPacket(e, isShout: true, isWhisper: false);
            }
            else if (e.Is(InWhisper))
            {
                ProcessChatPacket(e, isShout: false, isWhisper: true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing packet: {ex.Message}");
        }
    }

    private void ProcessChatPacket(Intercept e, bool isShout, bool isWhisper)
    {
        try
        {
            var index = e.Packet.Read<int>();
            var message = e.Packet.Read<string>();

            string senderName = $"User_{index}";

            // Debug: Check RoomManager state
            if (_roomManager.Room == null)
            {
                _logger.Debug($"RoomManager.Room is null, cannot resolve user {index}");
            }
            else
            {
                var userCount = _roomManager.Room.Users.Count();
                _logger.Debug($"Room has {userCount} users, looking for index {index}");

                if (_roomManager.Room.TryGetUserByIndex(index, out IUser? user) && user != null)
                {
                    senderName = user.Name;
                    _logger.Debug($"Resolved index {index} to {senderName}");
                }
                else
                {
                    _logger.Debug($"Could not find user with index {index}");
                }
            }

            ProcessChatMessage(senderName, index, message, isShout, isWhisper);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing chat packet: {ex.Message}");
        }
    }

    // Filtered users (wired bots, system messages)
    private static readonly HashSet<string> FilteredUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "User_0",
        "User_108"
    };

    private void ProcessChatMessage(string senderName, int senderId, string content, bool isShout, bool isWhisper)
    {
        // Skip our own messages
        var myUsername = _profileManager.UserData?.Name;
        if (!string.IsNullOrEmpty(myUsername) && senderName.Equals(myUsername, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Skip filtered users (wired bots, system)
        if (FilteredUsers.Contains(senderName))
        {
            _logger.Debug($"Filtered message from {senderName}: {content}");
            return;
        }

        // Skip unknown users (User_X pattern typically means we couldn't resolve the name)
        if (senderName.StartsWith("User_", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Debug($"Skipping unknown user: {senderName}");
            return;
        }

        if (string.IsNullOrWhiteSpace(content))
            return;

        var chatMessage = new ChatMessage
        {
            SenderName = senderName,
            SenderId = senderId,
            Content = content,
            IsShout = isShout,
            IsWhisper = isWhisper
        };

        _logger.Chat($"{senderName}: {content}");

        _conversationContext.AddMessage(chatMessage);

        OnChatReceived?.Invoke(chatMessage);

        // Debug: Log AI trigger conditions
        _logger.Debug($"AI Check - Enabled: {Enabled}, AISettings.IsEnabled: {_aiSettings.IsEnabled}, HasApiKey: {!string.IsNullOrEmpty(_aiSettings.ApiKey)}");

        if (Enabled && _aiSettings.IsEnabled && !string.IsNullOrEmpty(_aiSettings.ApiKey))
        {
            ScheduleResponse();
        }
        else
        {
            _logger.Debug("AI response not triggered - conditions not met");
        }
    }

    private void ScheduleResponse()
    {
        lock (_timerLock)
        {
            // If timer is already running and we've hit max resets, don't reset - let it fire
            if (_pendingResponse && _resetCount >= MaxResets)
            {
                _logger.Debug($"Max resets ({MaxResets}) reached, letting timer fire");
                return;
            }

            // If timer exists, this is a reset
            if (_pendingResponse)
            {
                _resetCount++;
                _logger.Debug($"Timer reset ({_resetCount}/{MaxResets})");
            }
            else
            {
                _resetCount = 0;
            }

            CancelPendingResponse();

            var delaySec = _random.Next(_aiSettings.MinResponseDelaySec, _aiSettings.MaxResponseDelaySec + 1);
            var delayMs = delaySec * 1000;

            _logger.Debug($"Scheduling response in {delaySec} seconds");

            _pendingResponse = true;
            _responseCts = new CancellationTokenSource();

            _responseTimer = new Timer(async _ =>
            {
                await GenerateAndSendResponse();
            }, null, delayMs, Timeout.Infinite);
        }
    }

    private void CancelPendingResponse()
    {
        lock (_timerLock)
        {
            _responseTimer?.Dispose();
            _responseTimer = null;
            _responseCts?.Cancel();
            _responseCts?.Dispose();
            _responseCts = null;
            _pendingResponse = false;
        }
    }

    private async Task GenerateAndSendResponse()
    {
        lock (_timerLock)
        {
            if (!_pendingResponse)
                return;
            _pendingResponse = false;
            _resetCount = 0; // Reset counter after response fires
        }

        if (!Enabled || !_aiSettings.IsEnabled)
            return;

        try
        {
            // Get character name from the connected username
            var characterName = _profileManager.UserData?.Name ?? "Visitante";

            // Check if directly mentioned (override random chance)
            var isDirectlyMentioned = _conversationContext.ContainsMention(characterName);

            // 30% random chance to respond, OR always respond if directly mentioned
            if (!isDirectlyMentioned)
            {
                var chance = _random.Next(100);
                if (chance >= 30)
                {
                    _logger.Debug($"Random chance {chance}% >= 30% - skipping (not mentioned)");
                    return; // Keep context for next time
                }
                _logger.Debug($"Random chance {chance}% < 30% - will respond");
            }
            else
            {
                _logger.Debug($"Directly mentioned by name - will respond");
            }

            _logger.Debug($"Generating AI response as {characterName}...");

            var response = await _openRouter.GenerateResponseAsync(
                _conversationContext,
                characterName
            );

            // Skip if empty or less than 5 characters - keep context for next time
            if (string.IsNullOrEmpty(response) || response.Length < 5)
            {
                _logger.Debug($"Skipping response: '{response}' - keeping context");
                return;
            }

            SendChat(response);

            // Only clear context after actually sending a message
            _conversationContext.Clear();
            _logger.Debug("Cleared conversation context after sending");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error generating response: {ex.Message}");
            // Clear context on error to avoid retrying with same bad context
            _conversationContext.Clear();
            _logger.Debug("Cleared conversation context after error");
        }
    }

    public void SendChat(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            _logger.Send($"Sending: {message}");
            Send(OutChat, message, 0, -1);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error sending chat: {ex.Message}");
        }
    }

    public void SendShout(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            _logger.Send($"Shouting: {message}");
            Send(OutShout, message, 0);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error sending shout: {ex.Message}");
        }
    }
}
