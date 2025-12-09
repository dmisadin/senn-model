namespace SENNModel.Models.Enums
{
    public enum RunNextAction
    {
        Stop,
        RestartFullRun,      // like GOTO 3333
        RestartIntegration   // like GOTO 202
    }
}
