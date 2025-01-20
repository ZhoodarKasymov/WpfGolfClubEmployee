using System.Windows;
using GolfClubSystem.Data;
using GolfClubSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views;

public partial class LoginWindow : Window
{
    private readonly UnitOfWork _unitOfWork;

    public LoginWindow()
    {
        InitializeComponent();
        _unitOfWork = new UnitOfWork();
        this.DataContext = new LoginViewModel();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (LoginViewModel)this.DataContext;
        
        if (string.IsNullOrWhiteSpace(viewModel.Username) || string.IsNullOrWhiteSpace(viewModel.Password))
        {
            MessageBox.Show("Заполните все поля перед входом.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var user = await _unitOfWork.UserRepository
            .GetAll()
            .FirstOrDefaultAsync(u => u.Username == viewModel.Username && u.Password == viewModel.Password);

        if (user == null)
        {
            MessageBox.Show("Не правильный логин либо пароль!","Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        switch (user.Role)
        {
            case "Admin":
            {
                var adminWindow = new MainAdminWindow();
                adminWindow.Show();
                break;
            }
            case "HR":
            {
                var hrWindow = new HRWindow();
                hrWindow.Show();
                break;
            }
        }

        Close();
    }
}