using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HomePodStreamer.Models
{
    public enum ConnectionState
    {
        Disconnected,
        Discovering,
        Connecting,
        Connected,
        Streaming,
        Error
    }

    public partial class HomePodDevice : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; }

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private ConnectionState _state = ConnectionState.Disconnected;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        // owntone output ID for API control
        public string OwntoneOutputId { get; set; } = string.Empty;

        // AirPlay mDNS TXT records (used for mDNS proxy into Docker)
        public Dictionary<string, string> AirPlayTxtRecords { get; set; } = new();

        // RAOP (_raop._tcp) service info for owntone compatibility
        public string RaopServiceName { get; set; } = string.Empty;
        public int RaopPort { get; set; }
        public Dictionary<string, string>? RaopTxtRecords { get; set; }
    }
}
