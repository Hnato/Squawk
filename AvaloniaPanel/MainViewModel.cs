using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Squawk.AvaloniaPanel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand ToggleGameCommand { get; }
        public ICommand ToggleWebCommand { get; }
        public ICommand ToggleBotsCommand { get; }

        public MainViewModel()
        {
            ToggleGameCommand = new RelayCommand(_ => { });
            ToggleWebCommand = new RelayCommand(_ => { });
            ToggleBotsCommand = new RelayCommand(_ => { });
        }

        void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
