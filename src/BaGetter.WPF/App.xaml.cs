using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using BaGetter.Git;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BaGetter.WPF;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private TextBlock? _statusText;
    private ProgressBar? _progressBar;
    private IHost? _host;

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
        IHost? host = null;
        GitRepositoryStatus? status = null;
        CancellationTokenRegistration startedRegistration = default;

        try
        {
            host = BaGetter.Program.CreateHostBuilder([]).Build();
            _host = host;
            status = host.Services.GetRequiredService<GitRepositoryStatus>();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            status.Changed += Status_Changed;
            ApplyProgress(status.Current);
            startedRegistration = lifetime.ApplicationStarted.Register(() =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    SetReady();
                    MessageBox.Show(
                        "BaGetter is ready and running in the background\nYou can access it from the system tray",
                        "BaGetter",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            });

            await BaGetter.Program.RunAsync(host, []);
        }
        catch (Exception ex)
        {
            ApplyProgress(new GitRepositoryProgress(GitRepositoryProgressPhase.Failed));
            MessageBox.Show($"An error occurred: {ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            startedRegistration.Dispose();
            if (status != null)
            {
                status.Changed -= Status_Changed;
            }

            _host = null;
            host?.Dispose();
            Dispatcher.Invoke(Shutdown);
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        _statusText = new TextBlock
        {
            Text = "Starting BaGetter...",
            Margin = new Thickness(0, 0, 0, 5),
        };
        _progressBar = new ProgressBar
        {
            Width = 190,
            Height = 6,
            Minimum = 0,
            Maximum = 100,
            IsIndeterminate = true,
        };
        var status = new MenuItem
        {
            Header = new StackPanel
            {
                Margin = new Thickness(4, 3, 4, 3),
                Children =
                {
                    _statusText,
                    _progressBar,
                },
            },
            Focusable = false,
            IsHitTestVisible = false,
        };
        var exit = new MenuItem
        {
            Header = "Exit"
        };
        exit.Click += async (_, __) =>
        {
            if (_host != null)
            {
                await _host.StopAsync();
            }
            else
            {
                Shutdown();
            }
        };

        menu.Items.Add(status);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        return menu;
    }

    private void Status_Changed(object? sender, GitRepositoryProgress progress)
    {
        Dispatcher.BeginInvoke(() => ApplyProgress(progress));
    }

    private void ApplyProgress(GitRepositoryProgress progress)
    {
        if (_statusText == null || _progressBar == null)
        {
            return;
        }

        _statusText.Text = progress.Phase switch
        {
            GitRepositoryProgressPhase.Idle => "Starting BaGetter...",
            GitRepositoryProgressPhase.Checking => "Checking Git repository...",
            GitRepositoryProgressPhase.Cloning => FormatTransfer("Cloning repository", progress),
            GitRepositoryProgressPhase.Fetching => FormatTransfer("Updating repository", progress),
            GitRepositoryProgressPhase.Indexing => FormatIndexing(progress),
            GitRepositoryProgressPhase.Synchronized => "Repository synchronized. Starting server...",
            GitRepositoryProgressPhase.Failed => "BaGetter failed to start",
            _ => throw new ArgumentOutOfRangeException(nameof(progress)),
        };
        _progressBar.IsIndeterminate = !progress.Percent.HasValue;
        _progressBar.Value = progress.Percent ?? 0;
        _trayIcon!.ToolTipText = _statusText.Text;
    }

    private void SetReady()
    {
        if (_statusText == null || _progressBar == null)
        {
            return;
        }

        _statusText.Text = "BaGetter is ready";
        _progressBar.IsIndeterminate = false;
        _progressBar.Value = 100;
        _trayIcon!.ToolTipText = "BaGetter is ready";
    }

    private static string FormatTransfer(string operation, GitRepositoryProgress progress)
    {
        if (!progress.Percent.HasValue)
        {
            return $"{operation}...";
        }

        return $"{operation}: {progress.Percent}% ({progress.ReceivedBytes / 1024d / 1024d:F1} MiB)";
    }

    private static string FormatIndexing(GitRepositoryProgress progress)
    {
        return progress.Percent.HasValue
            ? $"Indexing packages: {progress.Percent}% ({progress.Completed}/{progress.Total})"
            : "Indexing packages...";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
        => Shutdown();
}
