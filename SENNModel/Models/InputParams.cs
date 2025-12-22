using SENNModel.Models.Enums;

namespace SENNModel.Models;

public class InputParams
{
    // FIBER properties
    public int NNODES { get; set; } = 51;
    public int NLIN1 { get; set; } = 1;
    public int NLIN2 { get; set; } = 51;
    public int NODE1 { get; set; } = 22;
    public double DIAM { get; set; } = 0.0020;
    public double GAP { get; set; } = 0.00025;
    public double CM { get; set; } = 2.0;
    public double GM { get; set; } = 30.365;
    public double RHOI { get; set; } = 110.0;
    public double RHOE { get; set; } = 300.0;

    // STIMULUS properties
    public double XC { get; set; } = 0.0;
    public double YC { get; set; } = 1.0;
    public double XA { get; set; } = 100.0;
    public double YA { get; set; } = 100.0;
    public double WIREL { get; set; } = 0.85;
    public int IWAVE { get; set; } = 2;
    public double UIO { get; set; } = 16.0;
    public double XPD { get; set; } = 2.0;
    public double UIO2 { get; set; } = 0.0;
    public double XPD2 { get; set; } = 0.0;
    public double DELAY { get; set; } = 0.0;
    public double FREQ { get; set; } = 5.0;
    public double PHASE { get; set; } = 0.0;
    public double FREQ2 { get; set; } = 0.0;
    public double PHASE2 { get; set; } = 0.0;
    public double AMP2 { get; set; } = 0.0;
    public int NSINES { get; set; } = 27;
    public double DCOFF { get; set; } = 0.0;
    public double TAUS { get; set; } = 0.0;
    public double VREF { get; set; } = 0.0;
    public int NP { get; set; } = 1;
    public int FS { get; set; } = 0;
    public int S { get; set; } = 1;
    public int NTRP { get; set; } = 1;

    // CONTROL properties
    public int ITHR { get; set; } = 1;
    public double VTH { get; set; } = 80.0;
    public int NTHNODE { get; set; } = 3;
    public double DELT { get; set; } = 0.0002;
    public double DELT2M { get; set; } = 4.0;
    public double FINAL { get; set; } = 4.0;
    public int IPRNT { get; set; } = 50;

    // Optional descriptor
    public string? DESCRIPTOR { get; set; } = "SINUSOID";

    // Membrane model selection
    public MembraneModel MembraneModel { get; set; } = MembraneModel.FrankenhaeuserHuxley;
}
