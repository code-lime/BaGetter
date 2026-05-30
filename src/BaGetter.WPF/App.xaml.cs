using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;

namespace BaGetter.WPF;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/icon.ico")),
            ToolTipText = "BaGetter",
            ContextMenu = BuildMenu()
        };
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _ = Start();
    }

    private async Task Start()
    {
        try
        {
            MessageBox.Show("BaGetter is running in the background\nYou can access it from the system tray", "BaGetter", MessageBoxButton.OK, MessageBoxImage.Information);
            await BaGetter.Program.Main([]);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred: {ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            MessageBox.Show("BaGetter has stopped", "BaGetter", MessageBoxButton.OK, MessageBoxImage.Information);
            Dispatcher.Invoke(() => Shutdown());
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var exit = new MenuItem
        {
            Header = "Exit"
        };
        exit.Click += (_, __) =>
        {
            _trayIcon?.Dispose();
            Shutdown();
        };

        menu.Items.Add(exit);
        return menu;
    }


    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
        => Shutdown();
}
