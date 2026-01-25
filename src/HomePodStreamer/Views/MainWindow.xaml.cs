using System;
using System.ComponentModel;
using System.Windows;
using HomePodStreamer.ViewModels;
using HomePodStreamer.Models;

namespace HomePodStreamer.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _allowClose = false;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (!_allowClose)
            {
                // Don't actually close - minimize to tray
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                // Actually closing - dispose ViewModel
                _viewModel.Dispose();
            }
        }

        private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ShowWindow_Click(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private async void ToggleStreaming_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ToggleStreamingCommand.ExecuteAsync(null);
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            _allowClose = true;
            Application.Current.Shutdown();
        }

        private async void DeviceCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox &&
                checkBox.DataContext is HomePodStreamer.Models.HomePodDevice device)
            {
                await _viewModel.ToggleDeviceCommand.ExecuteAsync(device);
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
            }

            base.OnStateChanged(e);
        }
    }
}
