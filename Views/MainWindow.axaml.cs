using Avalonia.Controls;
using Avalonia.Interactivity;
using HabboGPTer.ViewModels;

namespace HabboGPTer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSendChatClick(object? sender, RoutedEventArgs e)
    {
        var chatInput = this.FindControl<TextBox>("ChatInputBox");
        if (chatInput == null || string.IsNullOrWhiteSpace(chatInput.Text))
            return;

        if (DataContext is MainViewModel vm)
        {
            vm.SendChatCommand.Execute(chatInput.Text).Subscribe();
            chatInput.Text = string.Empty;
        }
    }
}
