using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GolfClubSystem.Views.UserControlsViews;

namespace GolfClubSystem.ViewModels;

public class HRViewModel : INotifyPropertyChanged
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

    public HRViewModel()
    {
        NavigateCommand = new RelayCommand<string>(Navigate);
        CurrentView = new MainView(); // Initial page
        ActiveView = "Main"; // Set the initial active view
    }

    private void Navigate(string viewName)
    {
        ActiveView = viewName; // Update the active view
        switch (viewName)
        {
            case "Main":
                CurrentView = new MainView();
                break;
            case "History":
                CurrentView = new HistoryView();
                break;
            case "Workers":
                CurrentView = new WorkersView();
                break;
            case "Organizations":
                CurrentView = new OrganizationsView();
                break;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
