using System.Collections.Generic;

namespace HomePodStreamer.Models
{
    public class AppSettings
    {
        public List<SavedDevice> SavedDevices { get; set; } = new();
        public int GlobalVolume { get; set; } = 75;
        public bool AutoStartStreaming { get; set; } = false;
        public bool MinimizeToTrayOnStart { get; set; } = true;
        public string PreferredAudioDevice { get; set; } = string.Empty;
    }

    public class SavedDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}
