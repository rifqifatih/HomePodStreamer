using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using HomePodStreamer.Models;
using HomePodStreamer.Utils;

namespace HomePodStreamer.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsDir = Path.Combine(appData, "HomePodStreamer");
            Directory.CreateDirectory(settingsDir);
            _settingsPath = Path.Combine(settingsDir, "settings.json");
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Logger.Info("Settings file not found, creating default settings");
                    return new AppSettings();
                }

                var json = await File.ReadAllTextAsync(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                Logger.Info("Settings loaded successfully");
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings", ex);
                return new AppSettings();
            }
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_settingsPath, json);
                Logger.Info("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
            }
        }
    }
}
