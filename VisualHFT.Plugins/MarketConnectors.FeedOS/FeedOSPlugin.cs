using DotNetEnv;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VisualHFT.Commons.PluginManager;
using VisualHFT.DataRetriever;
using VisualHFT.UserSettings;
using VisualHFT.Model;
using MarketConnectors.FeedOS.Model;
using MarketConnectors.FeedOS.UserControls;
using MarketConnectors.FeedOS.ViewModel;
using VisualHFT.Helpers;
using VisualHFT.Commons.Pools;
using System.Threading;
using FeedOSAPI;
using FeedOSManaged;
using FeedOSAPI.Types;
using System.Diagnostics.Metrics;
using System.Data.Common;
using System.Reflection;
using System.Runtime;

namespace MarketConnectors.FeedOS
{
    public class FeedOSPlugin : BasePluginDataRetriever
    {
        private bool _disposed = false;
        private PlugInSettings _settings;
        private Connection _connection;

        public override string Name { get; set; } = "FeedOS Plugin";
        public override string Version { get; set; } = "1.0.0";
        public override string Description { get; set; } = "Connects to FeedOS API and retrieves MBL data.";
        public override string Author { get; set; } = "VisualHFT";
        public override ISetting Settings { get => _settings; set => _settings = (PlugInSettings)value; }
        public override Action CloseSettingWindow { get; set; }

        // Orderbooks
        private Dictionary<string, VisualHFT.Model.OrderBook> _localOrderBooks = new Dictionary<string, VisualHFT.Model.OrderBook>();
        private Dictionary<string, CancellationTokenSource> _ctDeltas = new Dictionary<string, CancellationTokenSource>();
        private Dictionary<string, CancellationTokenSource> _ctTrades = new Dictionary<string, CancellationTokenSource>();
        private Dictionary<string, long> _localOrderBooks_LastUpdate = new Dictionary<string, long>();
        private Timer _heartbeatTimer;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly ObjectPool<VisualHFT.Model.Trade> tradePool = new ObjectPool<VisualHFT.Model.Trade>();//pool of Trade objects
        private DataEventArgs tradeDataEvent = new DataEventArgs() { DataType = "Trades" }; //reusable object. So we avoid allocations
        private DataEventArgs marketDataEvent = new DataEventArgs() { DataType = "Market" };//reusable object. So we avoid allocations
        private DataEventArgs heartbeatDataEvent = new DataEventArgs() { DataType = "HeartBeats" };//reusable object. So we avoid allocations

        public FeedOSPlugin()
        {
            _connection = new Connection();
            Env.Load();

        }

        public override async Task StartAsync()
        {
            try
            {
                ConnectToFeedOS();
                await Task.Delay(1000);
                await base.StartAsync();
            }
            catch (Exception ex)
            {
                RaiseOnError(new VisualHFT.PluginManager.ErrorEventArgs { IsCritical = true, PluginName = Name, Exception = ex });
                await HandleConnectionLost();
            }
        }

        public override async Task StopAsync()
        {
            try
            {
                DisconnectFromFeedOS();
                await base.StopAsync();
            }
            catch (Exception ex)
            {
                RaiseOnError(new VisualHFT.PluginManager.ErrorEventArgs { IsCritical = false, PluginName = Name, Exception = ex });
            }
        }

        private void ConnectToFeedOS()
        {
            API.Init("VisualHFT.FeedOSPlugin");
            _connection = new Connection();

            uint result = _connection.Connect(_settings.HostIP, (uint)_settings.Port, _settings.Username, _settings.Password);
            if (result != 0)
            {
                throw new Exception($"Error connecting to FeedOS server: {API.ErrorString(result)} using this cred: {_settings.HostIP}" +
                    $"{(uint)_settings.Port}" + $"{_settings.Username}");
            }
        }

        private void DisconnectFromFeedOS()
        {
            _connection?.Disconnect();
            FeedOSManaged.API.Shutdown();
        }
        /*private void RaiseMarketDataEvent(VisualHFT.Model.OrderBook orderBook)
        {
            marketDataEvent.Data = orderBook;
            RaiseOnData(marketDataEvent);
        }*/

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!_disposed)
            {
                if (disposing)
                {
                    _connection?.Dispose();
                }
                _disposed = true;
            }
        }

        protected override void LoadSettings()
        {
            _settings = LoadFromUserSettings<PlugInSettings>();
            if (_settings == null)
            {
                InitializeDefaultSettings();
            }
            if (_settings.HostIP == "localhost")
            {
                _settings.HostIP = Env.GetString("HOST_IP");
            }
        }

        protected override void SaveSettings()
        {
            SaveToUserSettings(_settings);
        }

        protected override void InitializeDefaultSettings()
        {
            _settings = new PlugInSettings()
            {
                HostIP =  Env.GetString("HOST_IP"),
                Port =  Env.GetInt("PORT"),
                Username = Env.GetString("USERNAME"),
                Password = Env.GetString("PASSWORD"),
                RequestId = 0,
                Symbol = Env.GetString("INSTRUMENT_NAME"),
                Provider = new VisualHFT.Model.Provider { ProviderID = 1, ProviderName = "FeedOS" }
            };
            SaveToUserSettings(_settings);
        }

        public override object GetUISettings()
        {
            FeedOSSettingsView view = new FeedOSSettingsView();
            PlugInSettingsViewModel viewModel = new PlugInSettingsViewModel(() => CloseSettingWindow());
            viewModel.HostIP = _settings.HostIP;
            viewModel.Port = (int)_settings.Port;
            viewModel.Username = _settings.Username;
            viewModel.Password = _settings.Password;
            viewModel.RequestId = (int)_settings.RequestId;

            viewModel.UpdateSettingsFromUI = () =>
            {
                _settings.HostIP = viewModel.HostIP;
                _settings.Port = viewModel.Port;
                _settings.Username = viewModel.Username;
                _settings.Password = viewModel.Password;
                _settings.RequestId = viewModel.RequestId;
                SaveSettings();
                Task.Run(HandleConnectionLost);
            };
            view.DataContext = viewModel;
            return view;
        }
    }
}
