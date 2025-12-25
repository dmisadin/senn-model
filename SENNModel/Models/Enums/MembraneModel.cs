using System.ComponentModel;

namespace SENNModel.Models.Enums
{
    public enum MembraneModel
    {
        [Description("FH")]
        FrankenhaeuserHuxley = 0,  // Current default (FH)
        [Description("HH")]
        HodgkinHuxley = 1,          // HH
        [Description("CRRS")]
        ChiuRitchieRogartStagg = 2, // CRRS
        [Description("MRG")]
        McIntyreRichardsonGrill = 3 // MRG
    }
}
