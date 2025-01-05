using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace GolfClubSystem.Views;

public partial class AdminWindow : Window
{
    public AdminWindow()
    {
        InitializeComponent();
    }
    
    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }
}