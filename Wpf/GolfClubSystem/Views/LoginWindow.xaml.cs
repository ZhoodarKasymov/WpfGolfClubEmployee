﻿using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.ViewModels;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace GolfClubSystem.Views;

public partial class LoginWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly IConfigurationRoot _configuration = ((App)Application.Current)._configuration;
    private readonly LoadingService _loadingService;

    public LoginWindow()
    {
        InitializeComponent();
        var apiUrl = _configuration.GetSection("ApiUrl").Value
            ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiUrl)
        };
        _loadingService = LoadingService.Instance;
        DataContext = new LoginViewModel();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _httpClient.Dispose();
    }
 
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (LoginViewModel)DataContext;

        if (string.IsNullOrWhiteSpace(viewModel.Username) || string.IsNullOrWhiteSpace(viewModel.Password))
        {
            new DialogWindow("Ошибка", "Заполните все поля перед входом.").ShowDialog();
            return;
        }
        
        _loadingService.StartLoading();

        try
        {
            var response = await _httpClient.GetAsync($"api/Authorize/get-user-role?login={Uri.EscapeDataString(viewModel.Username)}&password={Uri.EscapeDataString(viewModel.Password)}");

            if (response.IsSuccessStatusCode)
            {
                var role = await response.Content.ReadAsStringAsync();

                switch (role)
                {
                    case "Admin":
                        var adminWindow = new MainAdminWindow();
                        adminWindow.Show();
                        break;
                    case "HR":
                        var hrWindow = new HRWindow();
                        hrWindow.Show();
                        break;
                    default:
                        new DialogWindow("Ошибка", "Неизвестная роль пользователя.").ShowDialog();
                        return;
                }

                Close();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                new DialogWindow("Ошибка", "Неправильный логин или пароль!").ShowDialog();
            }
            else
            {
                new DialogWindow("Ошибка", "Ошибка при подключении к серверу.").ShowDialog();
            }
        }
        catch (Exception ex)
        {
            new DialogWindow("Ошибка", $"Произошла ошибка: {ex.Message}").ShowDialog();
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginButton_Click(LoginButton, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}