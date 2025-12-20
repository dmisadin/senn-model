using CommunityToolkit.Mvvm.Input;
using SENNModel.Models;

namespace SENNModel.ViewModels;

public partial class MainWindowViewModel
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
