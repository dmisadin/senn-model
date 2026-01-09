using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SENNModel.Models;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SENNModel.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public InputParams InputParams { get; } = new InputParams();
    public bool ShouldUseTextFileInput { get; set; } = false;

    [ObservableProperty]
    private string consoleOutput = string.Empty;

    [ObservableProperty]
    private bool isRunning = false;

    private readonly object consoleLock = new object();

    private readonly SennRunner sennRunner;
    public MainWindowViewModel(SennRunner sennRunner)
    {
        this.sennRunner = sennRunner;
    }

    [RelayCommand]
    private async Task RunSimulation()
    {
        if (IsRunning)
            return; // Prevent multiple simultaneous runs

        // Clear console output
        ConsoleOutput = string.Empty;
        IsRunning = true;

        try
        {
            // Run simulation on background thread to keep UI responsive
            await Task.Run(() =>
            {
                // Redirect Console.Out to capture output (must be on same thread as simulation)
                var originalOut = Console.Out;
                var consoleWriter = new ConsoleTextWriter(this);
                Console.SetOut(consoleWriter);

                try
                {
                    if (ShouldUseTextFileInput)
                        sennRunner.Run(InputParams.MembraneModel);
                    else
                        sennRunner.Run(InputParams);
                }
                finally
                {
                    // Restore original Console.Out
                    Console.SetOut(originalOut);
                }
            });
        }
        catch (Exception ex)
        {
            AppendToConsole($"Error: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void AppendToConsole(string text)
    {
        lock (consoleLock)
        {
            // Ensure UI updates happen on the UI thread
            if (Dispatcher.UIThread.CheckAccess())
            {
                ConsoleOutput += text;
            }
            else
            {
                Dispatcher.UIThread.Post(() => ConsoleOutput += text);
            }
        }
    }

    private class ConsoleTextWriter : TextWriter
    {
        private readonly MainWindowViewModel viewModel;

        public ConsoleTextWriter(MainWindowViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            viewModel.AppendToConsole(value.ToString());
        }

        public override void Write(string? value)
        {
            if (value != null)
            {
                viewModel.AppendToConsole(value);
            }
        }

        public override void WriteLine(string? value)
        {
            if (value != null)
            {
                viewModel.AppendToConsole(value + Environment.NewLine);
            }
            else
            {
                viewModel.AppendToConsole(Environment.NewLine);
            }
        }
    }
}
