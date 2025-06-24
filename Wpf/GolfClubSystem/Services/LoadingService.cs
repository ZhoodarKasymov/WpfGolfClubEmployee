using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GolfClubSystem.Services;

public class LoadingService: INotifyPropertyChanged
{
    private static LoadingService _instance;
    private bool _isLoading;
    private int _loadingCount;

    private LoadingService()
    {
        _loadingCount = 0;
    }

    public static LoadingService Instance => _instance ??= new LoadingService();

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public void StartLoading()
    {
        lock (this)
        {
            _loadingCount++;
            IsLoading = _loadingCount > 0;
        }
    }

    public void StopLoading()
    {
        lock (this)
        {
            if (_loadingCount > 0)
            {
                _loadingCount--;
            }
            IsLoading = _loadingCount > 0;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}