using System.Windows;
using System.Windows.Controls;
using GolfClubSystem.Views.MainWindows;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void SendNotify_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new SendNotifyWindow();
        window.ShowDialog();
    }
}