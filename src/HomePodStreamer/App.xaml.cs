using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HomePodStreamer.ViewModels;
using HomePodStreamer.Views;
using HomePodStreamer.Services;

namespace HomePodStreamer
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        private void OnStartup(object sender, StartupEventArgs e)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // ViewModels
            services.AddSingleton<MainViewModel>();

            // Services
            services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            services.AddSingleton<IAudioEncoderService, AudioEncoderService>();
            services.AddSingleton<IAirPlayService, AirPlayProcessService>();
            services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // Views
            services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            base.OnExit(e);
        }
    }
}
