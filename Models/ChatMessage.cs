namespace HabboGPTer.Models;

public class ChatMessage
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string SenderName { get; init; } = string.Empty;
    public int SenderId { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsWhisper { get; init; }
    public bool IsShout { get; init; }

    public string SanitizedContent => SanitizeForAI(Content);

    private static string SanitizeForAI(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sanitized = input;

        if (sanitized.Length > 500)
            sanitized = sanitized[..500];

        var dangerousPatterns = new[]
        {
            "ignore previous",
            "ignore all previous",
            "disregard previous",
            "forget previous",
            "new instructions",
            "system:",
            "assistant:",
            "user:",
            "###",
            "<<<",
            ">>>",
            "[INST]",
            "[/INST]",
            "<|",
            "|>",
            "\\n\\n",
            "```",
            "===",
            "---"
        };

        foreach (var pattern in dangerousPatterns)
        {
            sanitized = sanitized.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
        }

        while (sanitized.Contains("  "))
            sanitized = sanitized.Replace("  ", " ");

        sanitized = sanitized.Replace("\n", " ").Replace("\r", " ");

        return sanitized.Trim();
    }

    public override string ToString()
    {
        var type = IsShout ? "SHOUT" : (IsWhisper ? "WHISPER" : "CHAT");
        return $"[{Timestamp:HH:mm:ss}] [{type}] {SenderName}: {Content}";
    }
}

public class ConversationContext
{
    private readonly List<ChatMessage> _messages = new();
    private readonly object _lock = new();
    private const int MaxMessages = 20;

    public void AddMessage(ChatMessage message)
    {
        lock (_lock)
        {
            _messages.Add(message);

            while (_messages.Count > MaxMessages)
            {
                _messages.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<ChatMessage> GetRecentMessages()
    {
        lock (_lock)
        {
            return _messages.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }

    public string BuildContextString()
    {
        lock (_lock)
        {
            if (_messages.Count == 0)
                return string.Empty;

            var lines = _messages.Select(m => $"{m.SenderName}: {m.SanitizedContent}");
            return string.Join("\n", lines);
        }
    }

    public bool ContainsMention(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        lock (_lock)
        {
            return _messages.Any(m =>
                m.Content.Contains(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
