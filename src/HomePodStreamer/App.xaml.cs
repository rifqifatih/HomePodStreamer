using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HomePodStreamer.ViewModels;
using HomePodStreamer.Views;
using HomePodStreamer.Services;
using HomePodStreamer.Utils;

namespace HomePodStreamer
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        // P/Invoke for console control handler
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        private delegate bool ConsoleCtrlDelegate(int ctrlType);

        private const int CTRL_C_EVENT = 0;
        private const int CTRL_BREAK_EVENT = 1;
        private const int CTRL_CLOSE_EVENT = 2;
        private const int SW_HIDE = 0;

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            // Set up Ctrl+C handler for graceful shutdown
            SetConsoleCtrlHandler(ConsoleCtrlHandler, true);

            // Hide console window if app was launched by double-clicking (not from terminal)
            HideConsoleIfNotLaunchedFromTerminal();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Start owntone in WSL and wait for it to be healthy
            try
            {
                var owntoneService = _serviceProvider.GetRequiredService<IOwntoneHostService>();
                Logger.Info("Starting owntone in WSL...");
                await owntoneService.StartAsync();
                await owntoneService.WaitForHealthyAsync(60);
                Logger.Info("Owntone is ready");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start owntone", ex);
                MessageBox.Show(
                    $"Failed to start owntone in WSL:\n\n{ex.Message}\n\n" +
                    "Ensure WSL2 Ubuntu is installed with owntone built from source.",
                    "HomePod Streamer - Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void HideConsoleIfNotLaunchedFromTerminal()
        {
            try
            {
                var consoleWindow = GetConsoleWindow();
                if (consoleWindow == IntPtr.Zero)
                    return;

                // Check if console is shared with parent process (terminal)
                uint[] processList = new uint[2];
                uint processCount = GetConsoleProcessList(processList, 2);

                // If only 1 process attached to console, we created it ourselves (double-click)
                // If 2+ processes, we're running from a terminal
                if (processCount == 1)
                {
                    ShowWindow(consoleWindow, SW_HIDE);
                    Logger.Info("Console window hidden (launched without terminal)");
                }
                else
                {
                    Logger.Info($"Console window visible (launched from terminal, {processCount} processes attached)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to manage console window", ex);
            }
        }

        private bool ConsoleCtrlHandler(int ctrlType)
        {
            if (ctrlType == CTRL_C_EVENT || ctrlType == CTRL_BREAK_EVENT || ctrlType == CTRL_CLOSE_EVENT)
            {
                Logger.Info($"Console control event received (type: {ctrlType}). Shutting down gracefully...");

                // Gracefully shutdown the application
                Dispatcher.Invoke(() =>
                {
                    // Get the MainViewModel and dispose it
                    var viewModel = _serviceProvider?.GetService<MainViewModel>();
                    viewModel?.Dispose();

                    // Shutdown the application
                    Shutdown();
                });

                // Return true to indicate we handled the event
                return true;
            }

            return false;
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // HTTP client for owntone API
            services.AddSingleton<HttpClient>();

            // Owntone services
            services.AddSingleton<IOwntoneApiClient, OwntoneApiClient>();
            services.AddSingleton<IOwntoneHostService, WslOwntoneService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();

            // Services
            services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            services.AddSingleton<IAudioEncoderService, AudioEncoderService>();
            services.AddSingleton<IAirPlayService, AirPlayOwntoneService>();
            services.AddSingleton<IDeviceDiscoveryService, HybridDeviceDiscoveryService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // Views
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop owntone on app exit.
            // Run on thread pool to avoid deadlock: StopAsync uses await internally,
            // and .GetAwaiter().GetResult() on the UI thread would block the
            // continuations from being dispatched back.
            try
            {
                var owntoneService = _serviceProvider?.GetService<IOwntoneHostService>();
                if (owntoneService != null)
                {
                    Logger.Info("Stopping owntone on exit...");
                    if (!Task.Run(() => owntoneService.StopAsync()).Wait(TimeSpan.FromSeconds(5)))
                    {
                        Logger.Warning("Owntone stop timed out after 5 seconds");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping owntone on exit", ex);
            }

            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            base.OnExit(e);
        }
    }
}
