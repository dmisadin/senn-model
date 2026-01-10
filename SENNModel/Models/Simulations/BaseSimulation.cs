using System;

namespace SENNModel.Models.Simulations
{
    public class BaseSimulation
    {
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
    }
}
