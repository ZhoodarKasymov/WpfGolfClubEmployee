using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using GolfClubSystem.Views;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GolfClubSystem;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private CancellationTokenSource _cts;
    public IConfigurationRoot _configuration;

    protected override void OnStartup(StartupEventArgs e)
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // Set the base directory to current directory
            .AddJsonFile("appsettings.json") // Read the connection string from appsettings.json
            .Build();

        base.OnStartup(e);

        CultureInfo culture = new CultureInfo("ru-RU");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag))
        );

        // Инициализация логгера
        Logger.Initialize();

        // Глобальная обработка ошибок
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;

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

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Error(ex, "Unhandled domain exception occurred.");
        ShowErrorMessage("Произошла критическая ошибка. Приложение будет закрыто.");
    }

    private void App_DispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
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