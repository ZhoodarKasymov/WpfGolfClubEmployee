using System.ComponentModel;

namespace GolfClubSystem.ViewModels;

public class LoginViewModel: INotifyPropertyChanged, IDataErrorInfo
{
    private string _username;
    private string _password;

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged(nameof(Username));
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged(nameof(Password));
        }
    }

    // Валидация обязательных полей
    public string this[string propertyName]
    {
        get
        {
            if (propertyName == nameof(Username))
            {
                if (string.IsNullOrWhiteSpace(Username))
                    return "Поле обязательно для заполнения";
            }

            if (propertyName == nameof(Password))
            {
                if (string.IsNullOrWhiteSpace(Password))
                    return "Поле обязательно для заполнения";
            }

            return null;
        }
    }

    public string Error => null;

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}