# HabboGPTer

AI-powered chat roleplay bot for Habbo Hotel using OpenRouter API and G-Earth extension framework.

## Description

HabboGPTer is a G-Earth extension that enables AI-powered roleplay conversations in Habbo Hotel. It uses OpenRouter API to generate natural, contextual responses based on room chat conversations. The bot automatically detects your Habbo username and uses it as the character name, responding naturally to conversations with a 30% random chance or when directly mentioned.

## Features

- 🤖 **AI-Powered Chat**: Uses OpenRouter API with free models for natural conversation
- 🔄 **Multi-Instance Support**: Manage multiple bot instances simultaneously
- 🔍 **Auto-Detection**: Automatically scans and connects to G-Earth instances
- 🎭 **Character Roleplay**: Uses your Habbo username as the character name
- 📊 **Real-time Logging**: Comprehensive logging system with categorized output
- 🎯 **Smart Response Logic**: 
  - 30% random chance to respond to conversations
  - Always responds when directly mentioned by name
  - Never starts conversations (only replies to existing context)
- 🛡️ **Safety Features**:
  - Filters out wired bots and system messages
  - Prevents prompt injection attacks
  - Response sanitization and length limiting
- ⚙️ **Configurable**: Adjustable response delays and AI settings

## Requirements

- .NET 8.0 SDK
- G-Earth (Habbo Hotel extension framework)
- OpenRouter API key (get one at [openrouter.ai](https://openrouter.ai))
- Windows, Linux, or macOS

## Installation

1. Clone or download this repository
2. Build the project:
   ```bash
   dotnet build
   ```
3. Copy the `extension.json` file to your G-Earth extensions folder
4. Copy the compiled executable to the same location

## Configuration

### Getting an OpenRouter API Key

1. Visit [openrouter.ai](https://openrouter.ai)
2. Sign up for a free account
3. Generate an API key
4. Copy your API key (starts with `sk-or-...`)

### Initial Setup

1. Launch HabboGPTer application
2. Enter your OpenRouter API key in the settings panel
3. Enable "AI Chat Enabled" toggle
4. Enable "Auto-detect" to automatically find G-Earth instances
5. Enable "Auto-connect" to automatically connect to new instances

### Settings

- **OpenRouter API Key**: Your API key from OpenRouter
- **AI Chat Enabled**: Master toggle for AI responses
- **Response Delay**: Min/Max delay in seconds before responding (default: 5-7 seconds)
- **Model**: Currently uses `openai/gpt-oss-120b:free` (free model)

## Usage

### Starting the Bot

1. Launch G-Earth and connect to Habbo Hotel
2. Launch HabboGPTer application
3. The application will automatically detect your G-Earth instance
4. Enable the "AI" checkbox for the instance you want to activate
5. Join a room in Habbo Hotel
6. The bot will start responding to conversations

### Manual Controls

- **Scan Now**: Manually scan for G-Earth instances
- **Connect**: Connect to a specific port manually
- **Disconnect**: Disconnect from an instance
- **Clear Log**: Clear the log output
- **Clear Chat**: Clear chat history and conversation context
- **Send**: Manually send a chat message

### Response Behavior

- The bot responds with a **30% random chance** to normal conversations
- The bot **always responds** when directly mentioned by your username
- The bot **never starts conversations** - it only replies to existing context
- Responses are limited to 100 characters
- Uses casual Brazilian Portuguese writing style (abbreviations, slang)
- No emojis or special formatting

## How It Works

1. **Connection**: HabboGPTer connects to G-Earth via localhost ports (9092-9099)
2. **Message Interception**: Intercepts incoming chat messages from the room
3. **Context Building**: Maintains conversation context (last 20 messages)
4. **User Filtering**: Filters out:
   - Your own messages
   - Wired bot messages
   - System messages
   - Unknown users (User_X pattern)
5. **AI Processing**: When conditions are met:
   - Checks if enabled and API key is valid
   - Calculates random chance or detects direct mention
   - Sends conversation context to OpenRouter API
   - Receives AI-generated response
6. **Response Sanitization**: Cleans and formats the response:
   - Removes markdown formatting
   - Limits length to 100 characters
   - Removes emojis and special characters
   - Filters AI self-identification
7. **Message Sending**: Sends the sanitized response to the room

## Technical Details

### Architecture

- **Framework**: .NET 8.0 with Avalonia UI
- **Extension Framework**: Xabbo.GEarth
- **UI Framework**: Avalonia with ReactiveUI (MVVM pattern)
- **API**: OpenRouter REST API
- **Logging**: Custom logger with file and UI output

### Project Structure

```
HabboGPTer/
├── Config/           # Configuration classes
├── Models/           # Data models (ChatMessage, ConversationContext)
├── Services/          # Core services (OpenRouter, Logger, ExtensionManager)
├── ViewModels/        # MVVM view models
├── Views/            # UI views (AXAML)
├── App.axaml         # Application definition
├── Program.cs        # Entry point
└── HabboGPTerExtension.cs  # Main extension class
```

### Key Components

- **HabboGPTerExtension**: Main extension class handling G-Earth integration
- **OpenRouterService**: Handles API communication with OpenRouter
- **ExtensionManager**: Manages multiple bot instances
- **GEarthScanner**: Scans for available G-Earth instances
- **Logger**: Comprehensive logging system
- **ConversationContext**: Maintains chat history for context

## Troubleshooting

### Bot Not Responding

1. Check that "AI Chat Enabled" is enabled in settings
2. Verify your OpenRouter API key is correct
3. Ensure the instance "AI" checkbox is enabled
4. Check the log output for error messages
5. Verify you have conversation context (the bot won't start conversations)

### Connection Issues

1. Ensure G-Earth is running
2. Check that G-Earth is listening on ports 9092-9099
3. Try manually scanning for instances
4. Check firewall settings

### API Errors

1. Verify your OpenRouter API key is valid
2. Check your API quota/limits on OpenRouter
3. Review log output for specific error messages
4. Ensure you have internet connectivity

### Response Quality Issues

- The bot uses a free model - responses may vary in quality
- Responses are limited to 100 characters for Habbo chat
- The bot maintains context from last 20 messages
- Context is cleared after each response

## Logging

Logs are saved to the `logs/` directory with timestamps. Log categories include:

- **DEBUG**: Detailed diagnostic information
- **INFO**: General information
- **CHAT**: Chat messages received
- **AI**: AI responses generated
- **SEND**: Messages sent
- **API**: API calls and responses
- **ERROR**: Error messages
- **WARNING**: Warning messages

## Safety & Ethics

- The bot includes prompt injection protection
- Filters out system messages and wired bots
- Response sanitization prevents harmful content
- Never reveals it's an AI or bot
- Respects Habbo Hotel terms of service

## License

This project is provided as-is for educational and personal use.

## Contributing

Contributions are welcome! Please ensure your code follows the existing style and includes appropriate error handling.

## Disclaimer

This tool is for educational purposes. Use responsibly and in accordance with Habbo Hotel's terms of service. The developers are not responsible for any misuse of this software.
