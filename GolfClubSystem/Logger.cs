using System.IO;
using Serilog;

namespace GolfClubSystem;

public static class Logger
{
    public static void Initialize()
    {
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        if (!Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console() // Для вывода в консоль
            .WriteTo.File(
                path: Path.Combine(logDirectory, "log-.txt"), // Файлы с разбиением по дате
                rollingInterval: RollingInterval.Day,          // Новый файл каждый день
                retainedFileCountLimit: 7,                    // Храним файлы за последние 7 дней
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        Log.Information("Инициализация логгера.");
    }

    public static void Shutdown()
    {
        Log.Information("Закрытие логгера.");
        Log.CloseAndFlush();
    }
}
