using SENNModel.Models.Enums;

namespace SENNModel.Models.Simulations
{
    public interface ISimulation
    {
        RunNextAction? ExecuteSimulationStep(SennState state);
    }
}
