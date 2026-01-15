using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using SENNModel.Models;
using SENNModel.Models.Enums;
using SENNModel.Models.IO;
using SENNModel.Models.Simulations;
using SENNModel.ViewModels;
using SENNModel.Views;
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SENNModel;

internal sealed class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        Services = ConfigureServices();

        // Headless path: run SennRunner and exit
        if (args.Any(a => a.Equals("--headless", StringComparison.OrdinalIgnoreCase)))
        {
            return await RunHeadlessFromArgs(args);
        }

        // GUI path
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static async Task<int> RunHeadlessFromArgs(string[] args)
    {
        var modelOpt = new Option<MembraneModel>(
            name: "model",
            aliases: ["--model", "-m"])
        {
            DefaultValueFactory = _ => MembraneModel.FrankenhaeuserHuxley
        };

        var outputDirOpt = new Option<DirectoryInfo>(
            name: "output-dir",
            aliases: ["--output-dir", "-o"])
        {
            Description = "Directory to write results into.",
            Required = false
        };

        var root = new RootCommand();
        root.Options.Add(modelOpt);
        root.Options.Add(outputDirOpt);

        root.SetAction((parseResult, ct) =>
        {
            var model = parseResult.GetValue(modelOpt);
            var outputDir = parseResult.GetValue(outputDirOpt);

            using var scope = Services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<SennRunner>();

            runner.Run(model, outputDir);

            Console.WriteLine("Headless run completed.");
            return Task.FromResult(0);
        });

        return await root.Parse(args.Where(a => !a.Equals("--headless", StringComparison.OrdinalIgnoreCase)).ToArray())
                        .InvokeAsync();
    }


    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<SennRunner>();
        services.AddScoped<FileImporter>();
        services.AddScoped<FileExporter>();

        services.AddKeyedTransient<ISimulation, FrankenhaeuserHuxleySimulation>(MembraneModel.FrankenhaeuserHuxley);
        services.AddKeyedTransient<ISimulation, HodgkinHuxleySimulation>(MembraneModel.HodgkinHuxley);
        services.AddKeyedTransient<ISimulation, ChiuRitchieRogartStaggSimulation>(MembraneModel.ChiuRitchieRogartStagg);
        services.AddKeyedTransient<ISimulation, McIntyreRichardsonGrillSimulation>(MembraneModel.McIntyreRichardsonGrill);

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>(sp =>
            new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() });

        return services.BuildServiceProvider();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

}
