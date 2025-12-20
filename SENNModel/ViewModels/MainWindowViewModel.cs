using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SENNModel.Models;

namespace SENNModel.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public InputParams InputParams { get; } = new InputParams();

    [RelayCommand]
    private void RunSimulation()
    {
        SennRunner.Run(InputParams);
    }
}
