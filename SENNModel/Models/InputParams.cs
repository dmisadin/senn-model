using CommunityToolkit.Mvvm.ComponentModel;

namespace SENNModel.Models;

public partial class InputParams : ObservableObject
{
    // FIBER properties
    [ObservableProperty] private int nnodes = 51;
    [ObservableProperty] private int nlin1 = 1;
    [ObservableProperty] private int nlin2 = 51;
    [ObservableProperty] private int node1 = 22;
    [ObservableProperty] private double diam = 0.0020;
    [ObservableProperty] private double gap = 0.00025;
    [ObservableProperty] private double cm = 2.0;
    [ObservableProperty] private double gm = 30.365;
    [ObservableProperty] private double rhoi = 110.0;
    [ObservableProperty] private double rhoe = 300.0;

    // STIMULUS properties
    [ObservableProperty] private double xc = 0.0;
    [ObservableProperty] private double yc = 1.0;
    [ObservableProperty] private double xa = 100.0;
    [ObservableProperty] private double ya = 100.0;
    [ObservableProperty] private double wirel = 0.85;
    [ObservableProperty] private int iwave = 2;
    [ObservableProperty] private double uio = 16.0;
    [ObservableProperty] private double xpd = 2.0;
    [ObservableProperty] private double uio2 = 0.0;
    [ObservableProperty] private double xpd2 = 0.0;
    [ObservableProperty] private double delay = 0.0;
    [ObservableProperty] private double freq = 5.0;
    [ObservableProperty] private double phase = 0.0;
    [ObservableProperty] private double freq2 = 0.0;
    [ObservableProperty] private double phase2 = 0.0;
    [ObservableProperty] private double amp2 = 0.0;
    [ObservableProperty] private int nsines = 27;
    [ObservableProperty] private double dcoff = 0.0;
    [ObservableProperty] private double taus = 0.0;
    [ObservableProperty] private double vref = 0.0;
    [ObservableProperty] private int np = 1;
    [ObservableProperty] private int fs = 0;
    [ObservableProperty] private int envSwitch = 1;
    [ObservableProperty] private int ntrp = 1;

    // CONTROL properties
    [ObservableProperty] private int ithr = 1;
    [ObservableProperty] private double vth = 80.0;
    [ObservableProperty] private int nthnode = 3;
    [ObservableProperty] private double delt = 0.0002;
    [ObservableProperty] private double delt2m = 4.0;
    [ObservableProperty] private double final = 4.0;
    [ObservableProperty] private int iprnt = 50;

    // Optional descriptor
    [ObservableProperty] private string? descriptor = "SINUSOID";

    // Helper method to get values in the format expected by SennRunner
    public int NNODES => Nnodes;
    public int NLIN1 => Nlin1;
    public int NLIN2 => Nlin2;
    public int NODE1 => Node1;
    public double DIAM => Diam;
    public double GAP => Gap;
    public double CM => Cm;
    public double GM => Gm;
    public double RHOI => Rhoi;
    public double RHOE => Rhoe;
    public double XC => Xc;
    public double YC => Yc;
    public double XA => Xa;
    public double YA => Ya;
    public double WIREL => Wirel;
    public int IWAVE => Iwave;
    public double UIO => Uio;
    public double XPD => Xpd;
    public double UIO2 => Uio2;
    public double XPD2 => Xpd2;
    public double DELAY => Delay;
    public double FREQ => Freq;
    public double PHASE => Phase;
    public double FREQ2 => Freq2;
    public double PHASE2 => Phase2;
    public double AMP2 => Amp2;
    public int NSINES => Nsines;
    public double DCOFF => Dcoff;
    public double TAUS => Taus;
    public double VREF => Vref;
    public int NP => Np;
    public int FS => Fs;
    public int S => EnvSwitch;
    public int NTRP => Ntrp;
    public int ITHR => Ithr;
    public double VTH => Vth;
    public int NTHNODE => Nthnode;
    public double DELT => Delt;
    public double DELT2M => Delt2m;
    public double FINAL => Final;
    public int IPRNT => Iprnt;
    public string? DESCRIPTOR => descriptor;
}
