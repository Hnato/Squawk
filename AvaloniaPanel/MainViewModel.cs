using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SquawkServer = Squawk.Server.Program;

namespace Squawk.AvaloniaPanel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<string> _logs = new ObservableCollection<string>();
        public ObservableCollection<string> Logs
        {
            get => _logs;
            set { _logs = value; OnPropertyChanged(); }
        }

        private bool _isGameRunning;
        public bool IsGameRunning { get => _isGameRunning; set { _isGameRunning = value; OnPropertyChanged(); } }

        private bool _isNetworkRunning;
        public bool IsNetworkRunning { get => _isNetworkRunning; set { _isNetworkRunning = value; OnPropertyChanged(); } }

        private bool _areBotsEnabled;
        public bool AreBotsEnabled { get => _areBotsEnabled; set { _areBotsEnabled = value; OnPropertyChanged(); } }

        public ICommand ToggleGameCommand { get; }
        public ICommand ToggleWebCommand { get; }
        public ICommand ToggleBotsCommand { get; }

        public MainViewModel()
        {
            // Initial states from the server core
            _isGameRunning = SquawkServer.IsGameRunning;
            _isNetworkRunning = SquawkServer.IsNetworkRunning;
            _areBotsEnabled = SquawkServer.AreBotsEnabled;

            ToggleGameCommand = new RelayCommand(_ => {
                if (SquawkServer.IsGameRunning) SquawkServer.StopGame(); else SquawkServer.StartGame();
            });

            ToggleWebCommand = new RelayCommand(_ => {
                if (SquawkServer.IsNetworkRunning) SquawkServer.StopNetwork(); else SquawkServer.StartNetwork();
            });

            ToggleBotsCommand = new RelayCommand(_ => {
                SquawkServer.SetBotsEnabled(!SquawkServer.AreBotsEnabled);
            });

            // Subscribe to server events to update UI
            SquawkServer.GameStateChanged += on => IsGameRunning = on;
            SquawkServer.NetworkStateChanged += on => IsNetworkRunning = on;
            SquawkServer.BotsStateChanged += on => AreBotsEnabled = on;

            // Subscribe to logs
            SquawkServer.Log.Subscribe(line => {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    Logs.Add(line.TrimEnd());
                    if (Logs.Count > 100) Logs.RemoveAt(0);
                });
            });
        }

        void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
