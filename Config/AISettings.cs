using System.Text.Json;

namespace HabboGPTer.Config;

public class AISettings
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "ai_settings.json"
    );

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "openai/gpt-oss-120b:free";

    public int MaxTokens { get; set; } = 100;

    public double Temperature { get; set; } = 0.8;

    public int MinResponseDelaySec { get; set; } = 5;

    public int MaxResponseDelaySec { get; set; } = 7;

    public string CharacterName { get; set; } = "Visitante";

    public string CharacterPersonality { get; set; } = "Uma pessoa amigavel e descontraida que gosta de conversar e fazer amigos no Habbo. Usa girias brasileiras e e bem humorado.";

    public bool IsEnabled { get; set; } = true;

    public event Action? OnSettingsChanged;

    public static AISettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AISettings>(json);
                return settings ?? new AISettings();
            }
        }
        catch { }

        return new AISettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public void SetApiKey(string apiKey)
    {
        ApiKey = apiKey;
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetModel(string model)
    {
        Model = model;
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetCharacter(string name, string personality)
    {
        CharacterName = name;
        CharacterPersonality = personality;
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetResponseDelay(int minSec, int maxSec)
    {
        MinResponseDelaySec = Math.Max(1, minSec);
        MaxResponseDelaySec = Math.Max(MinResponseDelaySec, maxSec);
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        Save();
        OnSettingsChanged?.Invoke();
    }
}
