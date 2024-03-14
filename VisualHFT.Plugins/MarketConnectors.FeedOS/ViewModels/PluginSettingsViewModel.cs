using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using VisualHFT.Helpers;
using MarketConnectors.FeedOS.Model;

namespace MarketConnectors.FeedOS.ViewModel
{
    public class PlugInSettingsViewModel : INotifyPropertyChanged
    {
        private PlugInSettings _pluginSettings;
        private Action _actionCloseWindow;

        public ICommand OkCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public Action UpdateSettingsFromUI { get; set; }

        private string _successMessage;
        public string SuccessMessage
        {
            get { return _successMessage; }
            set { _successMessage = value; OnPropertyChanged(nameof(SuccessMessage)); }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get { return _isConnected; }
            set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); }
        }

        // Connection Options
        public string HostIP
        {
            get => _pluginSettings.HostIP;
            set { _pluginSettings.HostIP = value; OnPropertyChanged(nameof(HostIP)); }
        }

        public int Port
        {
            get => _pluginSettings.Port;
            set { _pluginSettings.Port = value; OnPropertyChanged(nameof(Port)); }
        }

        public string Username
        {
            get => _pluginSettings.Username;
            set { _pluginSettings.Username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string Password
        {
            get => _pluginSettings.Password;
            set { _pluginSettings.Password = value; OnPropertyChanged(nameof(Password)); }
        }

        public int RequestId
        {
            get => _pluginSettings.RequestId;
            set { _pluginSettings.RequestId = value; OnPropertyChanged(nameof(RequestId)); }
        }

        // Change Instruments property to a single string
        private string _instrument;
        public string Instrument
        {
            get => _instrument;
            set { _instrument = value; OnPropertyChanged(nameof(Instrument)); }
        }

        public VisualHFT.Model.Provider Provider
        {
            get => _pluginSettings.Provider;
            set { _pluginSettings.Provider = value; OnPropertyChanged(nameof(Provider)); }
        }

        public AggregationLevel AggregationLevel
        {
            get => _pluginSettings.AggregationLevel;
            set { _pluginSettings.AggregationLevel = value; OnPropertyChanged(nameof(AggregationLevel)); }
        }
        // End of Connection Configuration

        public string ValidationMessage { get; set; }

        public PlugInSettingsViewModel(Action actionCloseWindow)
        {
            OkCommand = new RelayCommand<object>(ExecuteOkCommand);
            CancelCommand = new RelayCommand<object>(ExecuteCancelCommand);
            _actionCloseWindow = actionCloseWindow;

            // Initialize PluginSettings
            _pluginSettings = new PlugInSettings();
        }

        private void ExecuteOkCommand(object obj)
        {
            // Simulate a successful connection to FeedOS
            SimulateSuccessfulConnection();

            // Saving settings
            IsConnected = true; // Set IsConnected to true when connection is successful
            SuccessMessage = "FeedOS connected successfully!";
            UpdateSettingsFromUI?.Invoke();
            _actionCloseWindow?.Invoke();
        }

        private void SimulateSuccessfulConnection()
        {
            // Add your logic here to connect to FeedOS
            // If the connection is successful, set SuccessMessage
        }

        private void ExecuteCancelCommand(object obj)
        {
            // Closing the window
            _actionCloseWindow?.Invoke();
        }

        private void RaiseCanExecuteChanged()
        {
            (OkCommand as RelayCommand<object>)?.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
