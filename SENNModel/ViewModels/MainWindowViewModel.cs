using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SENNModel.Models;

namespace SENNModel.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private InputParams inputParams = new InputParams();

    [ObservableProperty] private string consoleOutput = "Console initialized...";

    [RelayCommand]
    private void RunSimulation()
    {
        SennRunner.Run(this.InputParams);
        //SennRunner.Run();
    }

}
