using System.Windows;
using GolfClubSystem.ViewModels;

namespace GolfClubSystem.Views
{
    public partial class HRWindow : Window
    {
        public HRWindow()
        {
            InitializeComponent();
            DataContext = new HRViewModel();
        }

        public void Exit(object sender, RoutedEventArgs routedEventArgs)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }
    }
}
