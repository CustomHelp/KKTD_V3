using System;
using System.IO;
using System.Windows;
using KKTD_V3.Models;
using KKTD_V3.Services;
using KKTD_V3.Services.Camera;
using KKTD_V3.Services.Conveyor;
using KKTD_V3.Services.Ejector;
using KKTD_V3.Services.Processing;
using KKTD_V3.ViewModels;
using KKTD_V3.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace KKTD_V3;

public partial class App : Application
{
    public static IHost? Host { get; private set; }

    public static T GetService<T>() where T : class
        => Host?.Services.GetRequiredService<T>()
           ?? throw new InvalidOperationException("Host not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        const string logDir = @"C:\CHP\Logs";
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDir, "kktd-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddLogging(b => b.AddSerilog(dispose: true));

                // INI service + Settings (aus C:\CHP\KKTD.ini geladen)
                services.AddSingleton<IniService>();
                services.AddSingleton<AppSettings>(sp =>
                    sp.GetRequiredService<IniService>().Load());

                // Core Services
                services.AddSingleton<ArticleManager>();
                services.AddSingleton<AdsService>();
                services.AddSingleton<BarcodeService>();
                services.AddSingleton<TeachService>();

                // Camera
                services.AddSingleton<ICameraService, CameraService>();

                // Processing pipeline
                services.AddSingleton<FlatFieldService>();
                services.AddSingleton<AreaMatcher>();
                services.AddSingleton<PyramidMatcher>();
                services.AddSingleton<ContourMatcher>();
                services.AddSingleton<ImageProcessingService>();

                // Ejector / Conveyor
                services.AddSingleton<EjectorService>();
                services.AddSingleton<ConveyorControlService>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<LiveViewModel>();
                services.AddSingleton<TeachViewModel>();
                services.AddSingleton<CatalogViewModel>();

                // Main window
                services.AddSingleton<MainWindow>();
            })
            .Build();

        Host.Start();

        var window = Host.Services.GetRequiredService<MainWindow>();
        window.DataContext = Host.Services.GetRequiredService<MainViewModel>();
        window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Host?.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        Host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
