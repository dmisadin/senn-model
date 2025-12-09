using System.IO;

namespace SENNModel.Models;

    // Holds all the global state corresponding to the old Fortran COMMON blocks
public class SennState
{
    // ----- Simple scalars from declarations -----

    // REAL LBH,LHN
    public double LBH;
    public double LHN;

    // REAL*8 DELTIN,DELTOT
    public double DELTIN;
    public double DELTOT;

    // INTEGER MES2(20),LENIN,LENOT
    public int[] MES2 = new int[21]; // Fortran 1..20 → C# [1..20]

    public int LENIN;
    public int LENOT;

    // INTEGER*2 NSINES
    public short NSINES;

    // LOGICAL CROSS,tflag,nfound
    public bool CROSS;
    public bool NFOUND;

    // INTEGER*2 NI,NNODES,NLIN1,NLIN2,NODE1,IWAVE,NP,FS,S,ITHR,
    //          NTHNODE,IPRNT,pltn
    public short NI;
    public short NNODES;
    public short NLIN1;
    public short NLIN2;
    public short NODE1;
    public short IWAVE;
    public short NP;
    public short FS;    // In Fortran, name starting with F would be REAL by default,
                        // but FS is declared as INTEGER*2 above, so we keep short.
    public short S;
    public short ITHR;
    public short NTHNODE;
    public short IPRNT;
    public short pltn;

    // DIMENSION Y(5100),DERY(5100),AUX(8,5100),PRMT(5)
    public double[] Y = new double[5101];      // 1..5100
    public double[] DERY = new double[5101];      // 1..5100
    public double[,] AUX = new double[9, 5101];   // 1..8, 1..5100
    public double[] PRMT = new double[6];         // 1..5

    // DIMENSION VM(20),TM(20),NN(20),UM(20),NXGT(20),IN(10)
    public double[] VM = new double[21];        // 1..20
    public double[] TM = new double[21];        // 1..20
    public short[] NN = new short[21];         // assume integer*2 semantics
    public double[] UM = new double[21];        // 1..20
    public int[] NXGT = new int[21];         // assume integer*2 semantics
    public int[] IN = new int[11];           // 1..10

    // ----- COMMON F,R,T,SODO,SODI,POTO,POTI,PNAB,PKB,PPB,GL,VL,ER -----
    public double F;
    public double R;
    public double T;
    public double SODO;
    public double SODI;
    public double POTO;
    public double POTI;
    public double PNAB;
    public double PKB;
    public double PPB;
    public double GL;
    public double VL;
    public double ER;

    // ----- COMMON VMAX,VTH,IPT,IPRNT,NLIN1,NLIN2,NON,XPD,XPD2,UIO,UIO2 -----
    public double VMAX;
    public double VTH;
    public int IPT;
    // IPRNT, NLIN1, NLIN2 already declared above (short)
    public short NON;
    public double XPD;
    public double XPD2;
    public double UIO;
    public double UIO2;

    // ----- COMMON NODE,NODE1 -----
    public int NODE;
    // NODE1 already declared

    // ----- COMMON IWAVE,FREQ,FREQ2,AMP2,ANGLE,ANGLE2,DCOFF,TAUS,DELAY -----
    // IWAVE already declared
    public double FREQ;
    public double FREQ2;
    public double AMP2;
    public double ANGLE;
    public double ANGLE2;
    public double DCOFF;
    public double TAUS;
    public double DELAY;

    // ----- COMMON PROD,PROD2,XMULT,PIMULT -----
    public double PROD;
    public double PROD2;
    public double XMULT;
    public double PIMULT;

    // ----- COMMON CA(4,3),CB(4,3),CGA,CGM,CCM,AREA,XA,YA,XC,YC,WIREL,EL,RHOE -----
    public double[,] CA = new double[5, 4]; // 1..4,1..3
    public double[,] CB = new double[5, 4]; // 1..4,1..3
    public double CGA;
    public double CGM;
    public double CCM;
    public double AREA;
    public double XA;
    public double YA;
    public double XC;
    public double YC;
    public double WIREL;
    public double EL;
    public double RHOE;

    // ----- COMMON TIM(1100),EPOT(1100),EPT(1100) -----
    public double[] TIM = new double[1101]; // 1..1100
    public double[] EPOT = new double[1101];
    public double[] EPT = new double[1101];

    // ----- COMMON UINA(1100),UIK(1100),UIP(1100),UIL(1100) -----
    public double[] UINA = new double[1101];
    public double[] UIK = new double[1101];
    public double[] UIP = new double[1101];
    public double[] UIL = new double[1101];

    // ----- COMMON TMAX,TEND,ITHR,INNGTT,NNGTT,NODEZ -----
    public double TMAX;
    public double TEND;
    // ITHR already declared
    public int INNGTT;
    public int NNGTT;
    public int NODEZ;

    // ----- COMMON /SWTCH/ FS,S,pltn,TT,tflag(6),Tp,nfound -----
    // FS, S, pltn, nfound already declared
    public double TT;
    public bool[] tflag = new bool[7]; // 1..6
    public double Tp;

    // ----- COMMON /FLD/ VREF,DIAM,NP,PL(128),PT(128) -----
    public double VREF;
    public double DIAM;
    // NP already declared
    public double[] PL = new double[129]; // 1..128
    public double[] PT = new double[129]; // 1..128

    // ----- COMMON /CBPARM/ AB,LBH,CMB,GMB -----
    // LBH already declared
    public double AB;
    public double CMB;
    public double GMB;

    // ----- COMMON /HPARAM/ AH,LHN,CMH,GMH -----
    // LHN already declared
    public double AH;
    public double CMH;
    public double GMH;

    // ----- COMMON /WPARM/ WB,DIAMB,WH,DIAMH,AN,GAP,GAH,GAB,SUMK(1100) -----
    public double WB;
    public double DIAMB;
    public double WH;
    public double DIAMH;
    public double AN;
    public double GAP;
    public double GAH;
    public double GAB;
    public double[] SUMK = new double[1101];

    // ----- COMMON /INARRAYS/ EPOTIN(1100),NSINES,SINEIN(1100,3),
    //       SINEIN2(1100,3),XIN(8001),YIN(8001),
    //       XCAL(2392000),YCAL(2392000),YINTERP(2400001) -----
    public double[] EPOTIN = new double[1101];            // 1..1100
    // NSINES already declared
    public double[,] SINEIN = new double[1101, 4];       // 1..1100,1..3
    public double[,] SINEIN2 = new double[1101, 4];       // 1..1100,1..3

    public double[] XIN = new double[8002];               // 1..8001
    public double[] YIN = new double[8002];               // 1..8001

    public double[] XCAL = new double[2392001];        // 1..2392000
    public double[] YCAL = new double[2392001];        // 1..2392000
    public double[] YINTERP = new double[2400002];        // 1..2400001


    // ===== From NAMELIST /FIBER/ =====
    //   NNODES, NLIN1, NLIN2, NODE1 already exist (shorts)
    //   DIAM, GAP already exist
    public double CM;    // membrane capacitance
    public double GM;    // membrane conductance
    public double RHOI;  // intracellular resistivity
    // RHOE already exists

    // ===== From NAMELIST /STIMULUS/ =====
    // XC, YC, XA, YA, WIREL exist
    // IWAVE, UIO, XPD, UIO2, XPD2, DELAY, FREQ exist
    public double PHASE;
    public double PHASE2;
    // NP, FS, S already exist
    public int NTRP;

    // ===== From NAMELIST /CONTROL/ =====
    public double DELT;
    public double DELT2;
    public double DELT2M;
    public double FINAL;
    // IPRNT already exists

    // ===== From NAMELIST /HILLOCK/ =====
    // WB, WH, DIAMB, DIAMH already exist

    // ===== Other scalars from this block =====
    public int IRUN;        // run counter
    public string namlst;   // CHARACTER*20 namlst in Fortran

    public double ELD; //ratio of internodal space to fiber diameter L/DIAM
    public double SDD; //ratio of axon and fiber diameters

    public double PI;
    public double TWOPI;
    public double FOURPI;
    public double PID180;

    public string Descriptor;
    public double URATIO;
    public int NDIM;         // dimension of the system
    public double UIOLD = -99999.0;     // previous stimulus amplitude
    public int IT;           // time-step counters / iteration counters
    public int ITA;
    public int ITB;

    public int K;    // size of active Y-region (2*NON+1)

    public int IA;
    public int IB;
    public double YMAX;
    public double YMIN;

    public int mpn;       // number of printable nodes
    public int IHLF;      // passed into RKGS

    public double VMAXO = -999999.0;
    public int NODOLD = 99999;
    public double TOLD = -9999.0;

    // Threshold tracking arrays (size 1101 so we can use 1..1100)
    public double[] VFLAG = new double[1101];
    public double[] TBT = new double[1101];
    public double[] TAT = new double[1101];
    public double[] VBT = new double[1101];
    public double[] VAT = new double[1101];
    public double[] TTIME = new double[1101];

    // Node where excitation starts (NODEXCIT)
    public int NodeExcit = 0;

    // Writers for unit 17 and 30 if you care about those outputs
    public StreamWriter? Out17 { get; set; }
    public StreamWriter? Out30 { get; set; }

    // ===== File handles (optional but convenient) =====
    public System.IO.TextWriter DataOutWriter;  // unit 66
    public System.IO.TextReader InParamReader;  // unit 7
    public System.IO.TextWriter XYInterpWriter;  // corresponds to Fortran unit 2

}