using DotNetEnv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

namespace MarketConnectors.FeedOS
{
    public class FeedOSPlugin : BasePluginDataRetriever
    {
        private bool _disposed = false;
        private PlugInSettings _settings;
        private Connection _connection;
        private Dictionary<string, VisualHFT.Model.OrderBook> _localOrderBooks = new Dictionary<string, VisualHFT.Model.OrderBook>();
        private FeedosOrderBookMapper _orderBookMapper;

        public override string Name { get; set; } = "FeedOS Plugin";
        public override string Version { get; set; } = "1.0.0";
        public override string Description { get; set; } = "Connects to FeedOS API and retrieves L2 data.";
        public override string Author { get; set; } = "VisualHFT";
        public override ISetting Settings { get => _settings; set => _settings = (PlugInSettings)value; }
        public override Action CloseSettingWindow { get; set; }
        private DataEventArgs marketDataEvent = new DataEventArgs() { DataType = "Market" };

        public FeedOSPlugin()
        {
            _connection = new Connection();
            Env.Load("C:\\Users\\safay\\Documents\\codespace\\git\\VisualHFT\\VisualHFT.Plugins\\MarketConnectors.FeedOS\\.env");
            _orderBookMapper = new FeedosOrderBookMapper();
        }

        public override async Task StartAsync()
        {
            try
            {
                ConnectToFeedOS();
                SubscribeToL2();
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
                UnsubscribeFromL2();
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
                throw new Exception($"Error connecting to FeedOS server: {API.ErrorString(result)}");
            }
        }

        private void DisconnectFromFeedOS()
        {
            _connection?.Disconnect();
            FeedOSManaged.API.Shutdown();
        }

        private void SubscribeToL2()
        {
            List<string> instruments = _settings.Instruments;
            Console.WriteLine(_settings.Instruments);
            _connection.SubscribeL2(instruments, false, (uint)_settings.RequestId);

            _connection.OrderBookSnapshotsHandler += OrderBookSnapshotsHandler;
            _connection.OrderBookRefreshHandler += OrderBookRefreshHandler;
            _connection.OrderBookDeltaRefreshHandler += OrderBookDeltaRefreshHandler;
        }

        private void UnsubscribeFromL2()
        {
            _connection.OrderBookSnapshotsHandler -= OrderBookSnapshotsHandler;
            _connection.OrderBookRefreshHandler -= OrderBookRefreshHandler;
            _connection.OrderBookDeltaRefreshHandler -= OrderBookDeltaRefreshHandler;

            List<string> instruments = _settings.Instruments;
            _connection.SubscribeL2Remove(instruments, (uint)_settings.RequestId);
        }

        private void OrderBookSnapshotsHandler(uint requestId, List<InstrumentStatusL2> instrumentStatuses)
        {
            foreach (var instrumentStatus in instrumentStatuses)
            {
                string normalizedSymbol = GetNormalizedSymbol(instrumentStatus.InstrumentCode);
                FeedOSAPI.Types.OrderBook orderBook = instrumentStatus.OrderBook;
                UpdateLocalOrderBook(normalizedSymbol, orderBook, instrumentStatus.InstrumentCode);
            }
        }

        private void OrderBookRefreshHandler(uint requestId, uint instrumentCode, ValueType serverUTCDateTime, OrderBookRefresh orderBookRefresh)
        {
            string normalizedSymbol = GetNormalizedSymbol(instrumentCode);
            UpdateLocalOrderBook(normalizedSymbol, orderBookRefresh);
        }

        private void OrderBookDeltaRefreshHandler(uint requestId, uint instrumentCode, ValueType serverUTCDateTime, OrderBookDeltaRefresh orderBookDeltaRefresh)
        {
            string normalizedSymbol = GetNormalizedSymbol(instrumentCode);
            UpdateLocalOrderBook(normalizedSymbol, orderBookDeltaRefresh);
        }

        private void UpdateLocalOrderBook(string normalizedSymbol, FeedOSAPI.Types.OrderBook orderBook, uint instrumentCode)
        {
            VisualHFT.Model.OrderBook visualHFTOrderBook = _orderBookMapper.MapOrderBook(orderBook, instrumentCode);
            _localOrderBooks[normalizedSymbol] = visualHFTOrderBook;
            RaiseOrderBookEvent(normalizedSymbol);
        }
        private void UpdateLocalOrderBook(string normalizedSymbol, OrderBookRefresh orderBookRefresh)
        {
            if (_localOrderBooks.ContainsKey(normalizedSymbol))
            {
                VisualHFT.Model.OrderBook visualHFTOrderBook = _orderBookMapper.UpdateOrderBook(_localOrderBooks[normalizedSymbol], orderBookRefresh);
                _localOrderBooks[normalizedSymbol] = visualHFTOrderBook;
                RaiseOrderBookEvent(normalizedSymbol);
            }
        }

        private void UpdateLocalOrderBook(string normalizedSymbol, OrderBookDeltaRefresh orderBookDeltaRefresh)
        {
            if (_localOrderBooks.ContainsKey(normalizedSymbol))
            {
                VisualHFT.Model.OrderBook visualHFTOrderBook = _orderBookMapper.UpdateOrderBook(_localOrderBooks[normalizedSymbol], orderBookDeltaRefresh);
                _localOrderBooks[normalizedSymbol] = visualHFTOrderBook;
                RaiseOrderBookEvent(normalizedSymbol);
            }
        }

        private void RaiseOrderBookEvent(string normalizedSymbol)
        {
            if (_localOrderBooks.ContainsKey(normalizedSymbol))
            {
                marketDataEvent.ParsedModel = new List<VisualHFT.Model.OrderBook> { _localOrderBooks[normalizedSymbol] };
                RaiseOnDataReceived(marketDataEvent);
            }
        }

        private string GetNormalizedSymbol(uint instrumentCode)
        {
            return instrumentCode.ToString();
        }

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
                HostIP = Env.GetString("HOST_IP"),
                Port = Env.GetInt("PORT"),
                Username = Env.GetString("USERNAME"),
                Password = Env.GetString("PASSWORD"),
                RequestId = 0,
                //Symbol = Env.GetString("INSTRUMENT_NAME"),
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