using SENNModel.Models.IO;
using System;

namespace SENNModel.Models.Simulations;

/// <summary>
/// When implementing a simulation of the SENN model, derive from this base class.
/// IMPORTANT: Extract methods from FrankenhaeuserHuxleySimulation.cs if they are common.
/// </summary>
public abstract class BaseSimulation
{
    protected readonly FileExporter fileExporter;

    protected BaseSimulation(FileExporter fileExporter)
    {
        this.fileExporter = fileExporter;
    }

    private void InitializeRun(SennState state)
    {
        // This corresponds to label 3333 "start of run"
        state.CROSS = false;
        state.NFOUND = false;

        // DO I = 1,6
        //   TFLAG(I) = .FALSE.
        // ENDDO
        for (int i = 1; i <= 6; i++)
        {
            state.tflag[i] = false;
        }
    }

    private void SetPhysicalConstants(SennState state)
    {
        // Gas constant, temperature, Faraday constant
        state.R = 8.3144;
        state.T = 295.16;     // absolute temperature (K)
        state.F = 96.487;

        // Ion concentrations (Na+, K+)
        state.SODO = 114.5;   // external sodium
        state.SODI = 13.74;   // internal sodium
        state.POTO = 2.5;     // external potassium
        state.POTI = 120.0;   // internal potassium

        // Ion permeability constants
        state.PNAB = 0.008;
        state.PKB = 0.0012;
        state.PPB = 0.00054;

        // Leak conductance + equilibrium potential
        state.GL = 30.3;
        state.VL = 0.0260430075;

        // Resting membrane potential
        state.ER = -70.0;

        // Axon / hillock geometry defaults
        state.TT = 1.0;    // 1 = terminated axon
        state.WB = 0.0;
        state.WH = 0.0;
        state.DIAMB = 0.0;
        state.DIAMH = 0.0;
    }

    private void SetIonicCurrentParameters(SennState state)
    {
        // ===== Alpha/Beta Coefficients (CA and CB arrays) =====
        // CA(x,y), CB(x,y) correspond to Fortran CA(1..4, 1..3), CB(1..4,1..3)

        // Row 1: h
        state.CA[1, 1] = 0.1;
        state.CB[1, 1] = 4.5;
        state.CA[1, 2] = -10.0;
        state.CB[1, 2] = 45.0;
        state.CA[1, 3] = 6.0;
        state.CB[1, 3] = 10.0;

        // Row 2: m
        state.CA[2, 1] = 0.36;
        state.CB[2, 1] = 0.40;
        state.CA[2, 2] = 22.0;
        state.CB[2, 2] = 13.0;
        state.CA[2, 3] = 3.0;
        state.CB[2, 3] = 20.0;

        // Row 3: p
        state.CA[3, 1] = 0.006;
        state.CB[3, 1] = 0.09;
        state.CA[3, 2] = 40.0;
        state.CB[3, 2] = -25.0;
        state.CA[3, 3] = 10.0;
        state.CB[3, 3] = 20.0;

        // Row 4: n
        state.CA[4, 1] = 0.02;
        state.CB[4, 1] = 0.05;
        state.CA[4, 2] = 35.0;
        state.CB[4, 2] = 10.0;
        state.CA[4, 3] = 10.0;
        state.CB[4, 3] = 10.0;

        // ===== General Membrane & Axon Parameters =====
        // NOTE: CM, RHOI, GM, and GAP are user-configurable parameters
        // They should NOT be set here as they come from input (GUI or file)
        // Setting them here would override user input values

        // These two appear to be geometric ratios (not user-configurable):
        state.ELD = 100.0;      // internodal length / fiber diameter
        state.SDD = 0.7;        // ratio axon diameter / fiber diameter

        // Threshold detection counter
        state.NNGTT = 0;
    }

    private void SetPiConstants(SennState state)
    {
        // Fortran DATA PI/3.141593/
        state.PI = 3.141593;

        // Derived expressions
        state.TWOPI = 2.0 * state.PI;
        state.FOURPI = 4.0 * state.PI;
        state.PID180 = state.PI / 180.0;
    }

    private void ValidateSettingsAndPrintHeader(SennState state)
    {
        var w = state.DataOutWriter;

        // TRAPS FOR IMPROPER VALUES OF FS AND S
        if (state.FS > 3 || state.FS < 0)
        {
            w?.WriteLine("FS must be integer 0, 1, 2 or 3");
            Console.WriteLine("FS must be integer 0, 1, 2 or 3");
            throw new InvalidOperationException("Invalid FS (must be 0, 1, 2, or 3).");
        }

        if (state.S > 1 || state.S < 0)
        {
            w?.WriteLine("S Must be integer 0 or 1");
            Console.WriteLine("S Must be integer 0 or 1");
            throw new InvalidOperationException("Invalid S (must be 0 or 1).");
        }

        // WARNING ABOUT USE OF FS=2
        if (state.FS == 2 && (state.IWAVE != 1 || state.NP != 1))
        {
            w?.WriteLine("FS = 2 VERIFIED ONLY FOR IWAVE = 1, SINGLE PULSE");
            Console.WriteLine("FS = 2 VERIFIED ONLY FOR IWAVE = 1, SINGLE PULSE");
            Console.WriteLine("Press Return to Continue, or Terminate Program");

            // Fortran PAUSE → wait for user input
            Console.ReadLine();
        }

        // Header line (FORMAT 600)
        const string header = " SPATIALLY EXTENDED NON-LINEAR NODE (SENN) MODEL, 2010";
        Console.WriteLine(header);
        w?.WriteLine(header);

        // Echo IWAVE, FS, S according to FS
        // Fortran format matches original spacing
        if (state.FS == 1 || state.FS == 2)
        {
            Console.WriteLine($"IWAVE  {state.IWAVE}    FS  {state.FS}");
            w?.WriteLine($"IWAVE  {state.IWAVE}    FS  {state.FS}");
        }
        else
        {
            // FS = 0 or 3
            Console.WriteLine($"IWAVE  {state.IWAVE}    FS  {state.FS}    S  {state.S}");
            w?.WriteLine($"IWAVE  {state.IWAVE}    FS  {state.FS}    S  {state.S}");
        }
    }

    /// <summary>
    /// USING A FOURTH ORDER RUNGE-KUTTA FORMULA WITH GILL MODIFICATION
    /// TO SOLVE A SYSTEM OF FIRST ORDER ORDINARY DIFFERENTIAL EQUATIONS
    /// WITH GIVEN INITIAL VALUES.
    /// </summary>
    public void RKGS(SennState s, int nDim)
    {
        // Aliases for convenience
        double[] Y = s.Y;
        double[] DERY = s.DERY;
        double[,] AUX = s.AUX;
        double[] PRMT = s.PRMT;

        // Local variables
        double[] A = new double[5];
        double[] B = new double[5];
        double[] C = new double[5];

        int INIT = 0;
        int I;
        int J;
        int ISTEP;
        int IEND;
        int ITEST;
        int IREC;
        int IMOD;
        int LL;

        double X, XEND, H;
        double HSAV = 0.0;
        double DELT = 0.0;

        // Initialize AUX(8, i) = 0.06666667 * DERY(i)
        for (I = 1; I <= nDim; I++)
        {
            AUX[8, I] = 0.06666667 * DERY[I];
        }

        X = PRMT[1];
        XEND = PRMT[2];
        H = PRMT[3];
        PRMT[5] = 0.0;

        // First derivative at initial point
        FCT(X, s);

        // Error test: IF(H*(XEND-X)) 38,37,2
        double prod = H * (XEND - X);
        if (prod < 0.0)
        {
            // label 38
            s.IHLF = 13;
            OutputStep(X, s, s.IHLF, nDim);
            return;
        }
        else if (Math.Abs(prod) < 1e-30)
        {
            // label 37
            s.IHLF = 12;
            OutputStep(X, s, s.IHLF, nDim);
            return;
        }

        // label 2: preparation for RK-Gill
        A[1] = 0.5;
        A[2] = 0.2928932;
        A[3] = 1.707107;
        A[4] = 0.1666667;

        B[1] = 2.0;
        B[2] = 1.0;
        B[3] = 1.0;
        B[4] = 2.0;

        C[1] = 0.5;
        C[2] = 0.2928932;
        C[3] = 1.707107;
        C[4] = 0.5;

        // Prepare first RK step
        for (I = 1; I <= nDim; I++)
        {
            AUX[1, I] = Y[I];     // initial Y
            AUX[2, I] = DERY[I];  // initial dY/dX
            AUX[3, I] = 0.0;
            AUX[6, I] = 0.0;
        }

        IREC = 0;
        H = 2.0 * H;
        s.IHLF = -1;
        ISTEP = 0;
        IEND = 0;

        // Main RKGS loop (label 4)
        while (true)
        {
            // label 4: adjust H for last step
            double prod2 = (X + H - XEND) * H;
            if (prod2 > 0.0)
            {
                // label 5: shrink H to hit XEND exactly
                H = XEND - X;
                IEND = 1;
            }
            else if (Math.Abs(prod2) < 1e-30)
            {
                // label 6: X+H == XEND
                IEND = 1;
            }
            // else prod2 < 0 => no change, IEND stays as-is

            // label 7: record initial values of the step
            OutputStep(X, s, IREC, nDim);

            // IF(PRMT(5)) 40,8,40
            if (Math.Abs(PRMT[5]) > 0.0)
            {
                // label 40: RETURN
                return;
            }

            // label 8:
            ITEST = 0;

            // label 9:
            while (true) // loop over ISTEP / inner RK cycles
            {
                ISTEP++;

                // label 10: inner RK-Gill loop
                J = 1;
                while (true)
                {
                    double AJ = A[J];
                    double BJ = B[J];
                    double CJ = C[J];

                    for (I = 1; I <= nDim; I++)
                    {
                        double R1 = H * DERY[I];
                        double R2 = AJ * (R1 - BJ * AUX[6, I]);
                        Y[I] += R2;
                        R2 = 3.0 * R2;
                        AUX[6, I] = AUX[6, I] + R2 - CJ * R1;
                    }

                    if (J < 4)
                    {
                        J++;
                        if (J <= 3)
                        {
                            // label 13
                            X = X + 0.5 * H;
                        }

                        // label 14: recompute derivative at new X,Y
                        FCT(X, s);
                        // back to label 10
                        continue;
                    }
                    // J == 4 → exit inner RK loop
                    break;
                }

                // label 15: test of accuracy
                if (ITEST == 0)
                {
                    // label 16: no previous half-step, no accuracy test yet
                    for (I = 1; I <= nDim; I++)
                        AUX[4, I] = Y[I];

                    ITEST = 1;
                    ISTEP = ISTEP + ISTEP - 2; // ISTEP = 2*ISTEP-2

                    // label 18: halve step
                    s.IHLF++;
                    X -= H;
                    H *= 0.5;

                    for (I = 1; I <= nDim; I++)
                    {
                        Y[I] = AUX[1, I];
                        DERY[I] = AUX[2, I];
                        AUX[6, I] = AUX[3, I];
                    }
                    // back to label 9
                    continue;
                }
                else
                {
                    // label 20: now we can test accuracy

                    IMOD = ISTEP / 2;
                    if (ISTEP != 2 * IMOD)
                    {
                        // label 21: ISTEP is odd
                        FCT(X, s);
                        for (I = 1; I <= nDim; I++)
                        {
                            AUX[5, I] = Y[I];
                            AUX[7, I] = DERY[I];
                        }
                        // back to 9
                        continue;
                    }

                    // label 23: ISTEP even → compute DELT
                    DELT = 0.0;
                    for (I = 1; I <= nDim; I++)
                    {
                        DELT += AUX[8, I] * Math.Abs(AUX[4, I] - Y[I]);
                    }

                    if (DELT > PRMT[4])
                    {
                        // label 25: error too large
                        if (s.IHLF < 10)
                        {
                            // label 26
                            for (I = 1; I <= nDim; I++)
                                AUX[4, I] = AUX[5, I];

                            ISTEP = ISTEP + ISTEP - 4;
                            X -= H;
                            IEND = 0;

                            // label 18: halve step again
                            s.IHLF++;
                            X -= H;
                            H *= 0.5;
                            for (I = 1; I <= nDim; I++)
                            {
                                Y[I] = AUX[1, I];
                                DERY[I] = AUX[2, I];
                                AUX[6, I] = AUX[3, I];
                            }
                            // back to 9
                            continue;
                        }
                        else
                        {
                            // label 36: too many halvings
                            s.IHLF = 11;
                            FCT(X, s);
                            // label 39:
                            OutputStep(X, s, s.IHLF, nDim);
                            return;
                        }
                    }
                    else
                    {
                        // label 28: result values are good
                        FCT(X, s);
                        for (I = 1; I <= nDim; I++)
                        {
                            AUX[1, I] = Y[I];
                            AUX[2, I] = DERY[I];
                            AUX[3, I] = AUX[6, I];
                            Y[I] = AUX[5, I];
                            DERY[I] = AUX[7, I];
                        }

                        if (INIT == 0)
                        {
                            HSAV = H;
                            INIT = 1;
                        }

                        if (Math.Abs(s.DELT2) < 1e-30)
                            s.DELT2 = H;

                        // OUTP at X-H (left side of step)
                        OutputStep(X - H, s, s.IHLF, nDim);

                        // Pulse-related step size logic (matches labels 306–307 block)
                        if (s.IWAVE != 15)
                        {
                            if (X > s.PT[s.NP])
                            {
                                H = s.DELT2;
                            }
                            else
                            {
                                bool insidePulse = false;
                                for (LL = 1; LL <= s.NP; LL++)
                                {
                                    if (X >= s.PL[LL] && X <= s.PT[s.NP])
                                    {
                                        insidePulse = true;
                                        break;
                                    }
                                }
                                if (insidePulse)
                                    H = HSAV;
                                else
                                    H = s.DELT2;
                            }
                        }

                        // IF (PRMT(5))40,30,40
                        if (Math.Abs(PRMT[5]) > 0.0)
                        {
                            OutputStep(X, s, s.IHLF, nDim); // label 39
                            return;
                        }

                        // label 30: accept step, update Y and DERY
                        for (I = 1; I <= nDim; I++)
                        {
                            Y[I] = AUX[1, I];
                            DERY[I] = AUX[2, I];
                        }
                        IREC = s.IHLF;

                        if (IEND != 0)
                        {
                            // label 39:
                            OutputStep(X, s, s.IHLF, nDim);
                            return;
                        }

                        // label 32: double step size if possible
                        s.IHLF--;
                        ISTEP /= 2;
                        H *= 2.0;

                        if (s.IHLF < 0)
                        {
                            // go back to label 4
                            break; // break out of the inner while( true ), resume outer loop
                        }

                        // label 33:
                        IMOD = ISTEP / 2;
                        if (ISTEP == 2 * IMOD)
                        {
                            // label 34:
                            if (DELT <= 0.02 * PRMT[4])
                            {
                                // label 35: double step again
                                s.IHLF--;
                                ISTEP /= 2;
                                H *= 2.0;
                            }
                        }

                        // back to label 4 (outer loop)
                        break;
                    }
                } // end if (ITEST)
            } // end inner while( true )
        } // end outer while(true)
    }

    protected abstract void OutputStep(double x, SennState s, int iHLF, int nDim);

    /// <summary>
    /// CALCULATION OF DERIVATIVES
    /// PROGRAM MODIFIED FROM THE ORIGINAL PROGRAM WRITTEN BY McNEAL(1976).
    /// THE SUBROUTINE HAS BEEN FURTHER MODIFIED AS FOLLOWS:
    /// LARGE EXP TEST REPLACED BY 0/0 TEST L'HOPITALS RULE
    /// CHANGED DEFN OF END POINTS  25 MAR 85
    /// FS SWITCH ADDED 3/17/87
    /// </summary>
    protected abstract void FCT(double x, SennState s);
}
