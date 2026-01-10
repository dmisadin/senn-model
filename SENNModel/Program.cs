using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using SENNModel.Models;
using SENNModel.Models.Enums;
using SENNModel.Models.IO;
using SENNModel.Models.Simulations;
using SENNModel.ViewModels;
using SENNModel.Views;
using System;

namespace SENNModel;

internal sealed class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static void Main(string[] args)
    {
        Services = ConfigureServices();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<SennRunner>();
        services.AddScoped<FileImporter>();
        services.AddKeyedTransient<ISimulation, FrankenhaeuserHuxleySimulation>(MembraneModel.FrankenhaeuserHuxley);
        services.AddKeyedTransient<ISimulation, HodgkinHuxleySimulation>(MembraneModel.HodgkinHuxley);

        // Register ViewModels
        services.AddSingleton<MainWindowViewModel>();

        // Register Views / Windows
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow
            {
                DataContext = sp.GetRequiredService<MainWindowViewModel>()
            });

        return services.BuildServiceProvider();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
