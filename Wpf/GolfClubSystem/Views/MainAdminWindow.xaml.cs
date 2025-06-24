using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

namespace GolfClubSystem.Views
{
    public partial class MainAdminWindow : Window, INotifyPropertyChanged
    {
        private object _currentView;
        private string _activeView;

        public object CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public string ActiveView
        {
            get => _activeView;
            set
            {
                _activeView = value;
                OnPropertyChanged();
            }
        }

        public ICommand NavigateCommand { get; }

        private void Navigate(string viewName)
        {
            ActiveView = viewName; // Update the active view
            switch (viewName)
            {
                case "Zones":
                    CurrentView = new ZonesView();
                    break;
                case "Shedules":
                    CurrentView = new SchedulerView();
                    break;
                case "AutoSchedule":
                    CurrentView = new AutoSchedulView();
                    break;
            }
        }

        public MainAdminWindow()
        {
            InitializeComponent();
            NavigateCommand = new RelayCommand<string>(Navigate);
            CurrentView = new ZonesView(); // Initial page
            ActiveView = "Zones"; // Set the initial active view
            DataContext = this;
        }

        public void Exit(object sender, RoutedEventArgs routedEventArgs)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}