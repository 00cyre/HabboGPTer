using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HabboGPTer.Views;
using HabboGPTer.ViewModels;

namespace HabboGPTer;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            var viewModel = new MainViewModel();

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            desktop.MainWindow.Show();

            desktop.ShutdownRequested += (s, e) =>
            {
                viewModel.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
