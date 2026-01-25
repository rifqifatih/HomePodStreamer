using System;
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

        // Internal handle for process management
        internal IntPtr NativeHandle { get; set; }
    }
}
