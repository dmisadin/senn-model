using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SENNModel.Models;

namespace SENNModel.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public InputParams InputParams { get; } = new InputParams();
    public bool ShouldUseTextFileInput { get; set; } = false;

    [RelayCommand]
    private void RunSimulation()
    {
        if (ShouldUseTextFileInput)
            SennRunner.Run();
        else
            SennRunner.Run(InputParams);
    }
}
