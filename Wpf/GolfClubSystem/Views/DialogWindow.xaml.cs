using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace GolfClubSystem.Views;

public partial class DialogWindow : Window, INotifyPropertyChanged
{
    private string _dialogDialogTitle;
    private string _text;
    private string _okButtonText;
    private string _closeButtonText;

    public string OkButtonText
    {
        get => _okButtonText;
        set => SetField(ref _okButtonText, value);
    }
    
    public string CloseButtonText
    {
        get => _closeButtonText;
        set => SetField(ref _closeButtonText, value);
    }
    
    public string DialogTitle
    {
        get => _dialogDialogTitle;
        set => SetField(ref _dialogDialogTitle, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }
    
    public DialogWindow(string dialogTitle, string text, string okButtonText = "OK", string closeButtonText = "Закрыть")
    {
        InitializeComponent();
        DataContext = this;
        DialogTitle = dialogTitle;
        Text = text;
        OkButtonText = okButtonText;
        CloseButtonText = closeButtonText;
    }
    
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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