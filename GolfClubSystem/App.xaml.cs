using System.IO;
using System.Windows;
using GolfClubSystem.Context;
using GolfClubSystem.Data;
using GolfClubSystem.Services;
using GolfClubSystem.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GolfClubSystem;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // Set the base directory to current directory
            .AddJsonFile("appsettings.json") // Read the connection string from appsettings.json
            .Build();
        
        base.OnStartup(e);
        
        // Инициализация логгера
        Logger.Initialize();
        
        // Глобальная обработка ошибок
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        
        // Init telegram
        var token = configuration.GetSection("Token").Value;
        InitializeBot(token);
        
        // Show login window
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exiting.");
        Logger.Shutdown();

        base.OnExit(e);
    }

    #region Private methods
    
    private async void InitializeBot(string token)
    {
        var telegramService = new TelegramService(token);
        await telegramService.StartListeningAsync();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Error(ex, "Unhandled domain exception occurred.");
        ShowErrorMessage("Произошла критическая ошибка. Приложение будет закрыто.");
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception occurred.");
        ShowErrorMessage("Произошла ошибка. Попробуйте снова.");
        e.Handled = true; // Не завершать приложение
    }

    private void ShowErrorMessage(string message)
    {
        MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    #endregion
}