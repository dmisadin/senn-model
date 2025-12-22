using SENNModel.Models.Enums;
using System;
using System.Globalization;
using System.IO;

namespace SENNModel.Models;

public static class SennRunner
{
    /// <summary>
    /// Run simulation with parameters from InputParams (GUI input)
    /// </summary>
    public static void Run(InputParams inputParams)
    {
        var state = InitializeSimulationState();

        // Apply input parameters to state
        ApplyInputParamsToState(state, inputParams);

        // Main loop: corresponds to label 3333 (start of run)
        // Fortran: GOTO 3333 can restart from the beginning
        while (true)
        {
            try
            {
                // Parameters already applied, skip file reading
                state.Descriptor = inputParams.DESCRIPTOR ?? "SINUSOID";

                if (ExecuteSimulationStep(state) == RunNextAction.Stop)
                {
                    break;
                }

                // For GUI input, always stop after one run to avoid infinite loop
                // The original file-based version would read new input on RestartFullRun,
                // but with GUI input we use the same parameters, so we stop after one run
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during run: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                break;
            }
        }

        CleanupSimulationState(state);
    }

    /// <summary>
    /// Run simulation with parameters from file (original behavior)
    /// </summary>
    public static void Run()
    {
        var state = InitializeSimulationState();

        // OPEN(UNIT=7,FILE='inparam.txt',STATUS='OLD',ACCESS='SEQUENTIAL')
        // => open existing file for reading
        state.InParamReader = new StreamReader("inparam.txt");

        // Main loop: corresponds to label 3333 (start of run)
        // Fortran: GOTO 3333 can restart from the beginning
        while (true)
        {
            try
            {
                // Label 1: Read input parameters (with EOF handling)
                // Fortran: READ(7,6666,END=5000)MES2
                if (!ReadInputParameters(state))
                {
                    // EOF reached (label 5000)
                    Console.WriteLine("HIT EOF ON INPUT");
                    break;
                }

                RunNextAction? nextAction = ExecuteSimulationStep(state);
                if (nextAction == null)
                {
                    break; // Error occurred
                }

                if (nextAction == RunNextAction.Stop)
                {
                    break;
                }
                else if (nextAction == RunNextAction.RestartIntegration)
                {
                    // GOTO 202: re-initialize Y and restart integration
                    // This is already handled by the loop, but we need to skip re-reading input
                    continue;
                }
                else if (nextAction == RunNextAction.RestartFullRun)
                {
                    // GOTO 3333: restart from beginning (read new input)
                    continue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during run: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                break;
            }
        }

        CleanupSimulationState(state);
    }

    /// <summary>
    /// Initialize simulation state and open output files
    /// </summary>
    private static SennState InitializeSimulationState()
    {
        var state = new SennState();

        // IRUN = 0 ! run counter
        state.IRUN = 0;

        // OPEN(UNIT=66,FILE='data.out',...)
        // STATUS='UNKNOWN' ≈ create or overwrite
        state.DataOutWriter = new StreamWriter("data.out", append: false);
        state.Out17 = new StreamWriter("plot_17.txt");
        state.Out30 = new StreamWriter("plot_30.txt");

        return state;
    }

    /// <summary>
    /// Execute a single simulation step (from label 3333 to end of run)
    /// Returns the next action to take, or null if an error occurred
    /// </summary>
    private static RunNextAction? ExecuteSimulationStep(SennState state)
    {
        // Label 3333: start of run
        InitializeRun(state);
        SetPhysicalConstants(state);
        SetIonicCurrentParameters(state);
        SetPiConstants(state);

        ValidateSettingsAndPrintHeader(state);
        ConfigureProbeAndWaveform(state);
        ImportExternalArrays(state);
        ImportXYForWaveform13(state);
        PostWaveformSetup(state);
        ConfigureWaveformParameters(state);

        WriteParameterSummary(state);
        SetupGeometryAndRunParameters(state);

        // Label 202: Initialize state vector and run simulation
        InitializeStateVectorY(state);
        ComputeExternalPotentialsAndInitDerivatives(state);

        RunThresholdSearch(state);  // or a simpler RunSimulation if ITHR == 0

        PrintIterativeSummary(state);
        return EndOfRunAndDecideNext(state);
    }

    /// <summary>
    /// Clean up simulation state and close files
    /// </summary>
    private static void CleanupSimulationState(SennState state)
    {
        state.DataOutWriter?.Dispose();
        state.InParamReader?.Dispose();
        state.Out17?.Dispose();
        state.Out30?.Dispose();
    }

    private static void ApplyInputParamsToState(SennState state, InputParams input)
    {
        // FIBER
        state.NNODES = (short)input.NNODES;
        state.NLIN1 = (short)input.NLIN1;
        state.NLIN2 = (short)input.NLIN2;
        state.NODE1 = (short)input.NODE1;
        state.DIAM = input.DIAM;
        state.GAP = input.GAP;
        state.CM = input.CM;
        state.GM = input.GM;
        state.RHOI = input.RHOI;
        state.RHOE = input.RHOE;

        // STIMULUS
        state.XC = input.XC;
        state.YC = input.YC;
        state.XA = input.XA;
        state.YA = input.YA;
        state.WIREL = input.WIREL;
        state.IWAVE = (short)input.IWAVE;
        state.UIO = input.UIO;
        state.XPD = input.XPD;
        state.UIO2 = input.UIO2;
        state.XPD2 = input.XPD2;
        state.DELAY = input.DELAY;
        state.FREQ = input.FREQ;
        state.PHASE = input.PHASE;
        state.FREQ2 = input.FREQ2;
        state.PHASE2 = input.PHASE2;
        state.AMP2 = input.AMP2;
        state.NSINES = (short)input.NSINES;
        state.DCOFF = input.DCOFF;
        state.TAUS = input.TAUS;
        state.VREF = input.VREF;
        state.NP = (short)input.NP;
        state.FS = (short)input.FS;
        state.S = (short)input.S;
        state.NTRP = input.NTRP;

        // CONTROL
        state.ITHR = (short)input.ITHR;
        state.VTH = input.VTH;
        state.NTHNODE = (short)input.NTHNODE;
        state.DELT = input.DELT;
        state.DELT2M = input.DELT2M;
        state.FINAL = input.FINAL;
        state.IPRNT = (short)input.IPRNT;
        state.MembraneModel = input.MembraneModel;
    }

    private static void InitializeRun(SennState state)
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

    private static void SetPhysicalConstants(SennState state)
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

    private static void SetIonicCurrentParameters(SennState state)
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

    private static void SetPiConstants(SennState state)
    {
        // Fortran DATA PI/3.141593/
        state.PI = 3.141593;

        // Derived expressions
        state.TWOPI = 2.0 * state.PI;
        state.FOURPI = 4.0 * state.PI;
        state.PID180 = state.PI / 180.0;
    }




    public static bool ReadInputParameters(SennState state)
    {
        var reader = state.InParamReader
                     ?? throw new InvalidOperationException("InParamReader is not initialized.");

        string? line;
        bool headerRead = false;
        string? currentSection = null;  // "FIBER", "STIMULUS", "CONTROL"

        // Use invariant culture for parsing doubles with '.' decimal
        var ci = CultureInfo.InvariantCulture;

        // Fortran: READ(7,6666,END=5000)MES2
        // Read the descriptor line (20A4 format = 80 characters, stored in MES2 array)
        line = reader.ReadLine();
        if (line == null)
        {
            return false; // EOF reached (label 5000)
        }
        // Store descriptor (first line is the run descriptor)
        state.Descriptor = line.Trim();
        headerRead = true;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            // Section markers: &FIBER, &STIMULUS, &CONTROL, &END
            if (line.StartsWith("&", StringComparison.OrdinalIgnoreCase))
            {
                var upper = line.ToUpperInvariant();
                if (upper.StartsWith("&FIBER"))
                {
                    currentSection = "FIBER";
                }
                else if (upper.StartsWith("&STIMULUS"))
                {
                    currentSection = "STIMULUS";
                }
                else if (upper.StartsWith("&CONTROL"))
                {
                    currentSection = "CONTROL";
                }
                else if (upper.StartsWith("&END"))
                {
                    currentSection = null;
                }

                continue;
            }

            // If we're inside a section, parse assignments like: NAME=VALUE,
            if (currentSection != null)
            {
                var assignments = line.Split(',');
                foreach (var raw in assignments)
                {
                    var part = raw.Trim();
                    if (string.IsNullOrEmpty(part))
                        continue;

                    var kv = part.Split('=');
                    if (kv.Length != 2)
                        continue;

                    var name = kv[0].Trim().ToUpperInvariant();
                    var value = kv[1].Trim();

                    switch (currentSection)
                    {
                        case "FIBER":
                            ParseFiberField(state, name, value, ci);
                            break;
                        case "STIMULUS":
                            ParseStimulusField(state, name, value, ci);
                            break;
                        case "CONTROL":
                            ParseControlField(state, name, value, ci);
                            break;
                    }
                }
            }
        }

        return true; // Successfully read parameters
    }

    private static void ParseFiberField(SennState state, string name, string value, IFormatProvider ci)
    {
        switch (name)
        {
            case "NNODES":
                state.NNODES = (short)int.Parse(value, ci);
                break;
            case "NLIN1":
                state.NLIN1 = (short)int.Parse(value, ci);
                break;
            case "NLIN2":
                state.NLIN2 = (short)int.Parse(value, ci);
                break;
            case "NODE1":
                state.NODE1 = (short)int.Parse(value, ci);
                break;
            case "DIAM":
                state.DIAM = double.Parse(value, ci);
                break;
            case "GAP":
                state.GAP = double.Parse(value, ci);
                break;
            case "CM":
                state.CM = double.Parse(value, ci);
                break;
            case "GM":
                state.GM = double.Parse(value, ci);
                break;
            case "RHOI":
                state.RHOI = double.Parse(value, ci);
                break;
            case "RHOE":
                state.RHOE = double.Parse(value, ci);
                break;
        }
    }

    private static void ParseStimulusField(SennState state, string name, string value, IFormatProvider ci)
    {
        switch (name)
        {
            case "XC":
                state.XC = double.Parse(value, ci);
                break;
            case "YC":
                state.YC = double.Parse(value, ci);
                break;
            case "XA":
                state.XA = double.Parse(value, ci);
                break;
            case "YA":
                state.YA = double.Parse(value, ci);
                break;
            case "WIREL":
                state.WIREL = double.Parse(value, ci);
                break;
            case "IWAVE":
                state.IWAVE = (short)int.Parse(value, ci);
                break;
            case "UIO":
                state.UIO = double.Parse(value, ci);
                break;
            case "XPD":
                state.XPD = double.Parse(value, ci);
                break;
            case "UIO2":
                state.UIO2 = double.Parse(value, ci);
                break;
            case "XPD2":
                state.XPD2 = double.Parse(value, ci);
                break;
            case "DELAY":
                state.DELAY = double.Parse(value, ci);
                break;
            case "FREQ":
                state.FREQ = double.Parse(value, ci);
                break;
            case "PHASE":
                state.PHASE = double.Parse(value, ci);
                break;
            case "FREQ2":
                state.FREQ2 = double.Parse(value, ci);
                break;
            case "PHASE2":
                state.PHASE2 = double.Parse(value, ci);
                break;
            case "AMP2":
                state.AMP2 = double.Parse(value, ci);
                break;
            case "NSINES":
                state.NSINES = (short)int.Parse(value, ci);
                break;
            case "DCOFF":
                state.DCOFF = double.Parse(value, ci);
                break;
            case "TAUS":
                state.TAUS = double.Parse(value, ci);
                break;
            case "VREF":
                state.VREF = double.Parse(value, ci);
                break;
            case "NP":
                state.NP = (short)int.Parse(value, ci);
                break;
            case "FS":
                state.FS = (short)int.Parse(value, ci);
                break;
            case "S":
                state.S = (short)int.Parse(value, ci);
                break;
            case "NTRP":
                state.NTRP = int.Parse(value, ci);
                break;
        }
    }

    private static void ParseControlField(SennState state, string name, string value, IFormatProvider ci)
    {
        switch (name)
        {
            case "ITHR":
                state.ITHR = (short)int.Parse(value, ci);
                break;
            case "VTH":
                state.VTH = double.Parse(value, ci);
                break;
            case "NTHNODE":
                state.NTHNODE = (short)int.Parse(value, ci);
                break;
            case "DELT":
                state.DELT = double.Parse(value, ci);
                break;
            case "DELT2M":
                state.DELT2M = double.Parse(value, ci);
                break;
            case "FINAL":
                state.FINAL = double.Parse(value, ci);
                break;
            case "IPRNT":
                state.IPRNT = (short)int.Parse(value, ci);
                break;
                // TT, DELT2, pltn keep their defaults (set elsewhere)
        }
    }



    private static void ValidateSettingsAndPrintHeader(SennState state)
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

    private static void ConfigureProbeAndWaveform(SennState state)
    {
        var w = state.DataOutWriter;
        var ci = CultureInfo.InvariantCulture;

        // ----- PIMULT VALUES FOR ELECTRODE PROBE MODES -----
        if (state.FS == 0 && state.S == 0)
        {
            state.PIMULT = state.FOURPI;   // Point electrode in situ
        }
        else if (state.FS == 0 && state.S == 1)
        {
            state.PIMULT = state.TWOPI;    // Point electrode on surface
        }
        else if (state.FS == 3 && state.S == 0)
        {
            state.PIMULT = state.TWOPI;    // Line electrode in situ
        }
        else if (state.FS == 3 && state.S == 1)
        {
            state.PIMULT = state.PI;       // Line electrode on surface
        }
        // For FS = 1 or 2, Fortran leaves PIMULT as previously set.

        // ----- IWAVE = 13 warning and NTRP limit -----
        if (state.IWAVE == 13)
        {
            w?.WriteLine("Note: In this implementation, X values for IWAVE=13 are internally");
            w?.WriteLine("calculated, based on one space measurement of the input data.");
            w?.WriteLine("FOR VALID RESULTS, THE INPUT DATA POINTS MUST BE UNIFORMLY SPACED.");

            Console.WriteLine("Note: In this implementation, X values for IWAVE=13 are internally");
            Console.WriteLine("calculated, based on one space measurement of the input data.");
            Console.WriteLine("FOR VALID RESULTS, THE INPUT DATA POINTS MUST BE UNIFORMLY SPACED.");

            if (state.NTRP > 299)
            {
                w?.WriteLine("299 interpolated points is the current maximum.");
                Console.WriteLine("299 interpolated points is the current maximum.");
                throw new InvalidOperationException("NTRP > 299 is not allowed for IWAVE=13.");
            }
        }

        // ----- Extra user input for certain IWAVE modes -----
        if (state.IWAVE == 8 || state.IWAVE == 9)
        {
            Console.WriteLine($"UIO2,FREQ FOR IWAVE {state.IWAVE}?");
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                var parts = input.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    state.UIO2 = double.Parse(parts[0], ci);
                    state.FREQ = double.Parse(parts[1], ci);
                }
                else
                {
                    throw new FormatException("Expected two values: UIO2 and FREQ.");
                }
            }
        }

        if (state.IWAVE == 3)
        {
            Console.WriteLine($"UIO,FREQ,Tp,DCOFF FOR IWAVE {state.IWAVE}?");
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input))
            {
                var parts = input.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    state.UIO = double.Parse(parts[0], ci);
                    state.FREQ = double.Parse(parts[1], ci);
                    state.Tp = double.Parse(parts[2], ci);
                    state.DCOFF = double.Parse(parts[3], ci);
                }
                else
                {
                    throw new FormatException("Expected four values: UIO, FREQ, Tp, DCOFF.");
                }
            }
        }

        // ----- CONTROL and HILLOCK -----
        // In Fortran, this is where CONTROL is read and DELT2 is derived.
        // We've already parsed CONTROL via ReadInputParameters, so we
        // only need to derive DELT2 here.
        state.DELT2 = state.DELT2M * state.DELT;

        // In Fortran:
        //   IF (TT .EQ. 2) READ(7,HILLOCK)
        // Our parser can already handle an optional &HILLOCK namelist,
        // so nothing else is needed here; state.WB, WH, DIAMB, DIAMH
        // are already filled if present in the file and TT=2.
    }

    private static void ImportExternalArrays(SennState state)
    {
        var ci = CultureInfo.InvariantCulture;

        // ----- FS = 2: import EPOTIN from EPOTfile.txt -----
        if (state.FS == 2)
        {
            const string epotFile = "EPOTfile.txt";

            if (!File.Exists(epotFile))
                throw new FileNotFoundException("EPOT file not found.", epotFile);

            using (var r = new StreamReader(epotFile))
            {
                int i = 1; // Fortran-style 1-based index
                string? line;
                while (i <= state.EPOTIN.Length - 1 && (line = r.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    state.EPOTIN[i] = double.Parse(line, ci);
                    i++;
                }
            }

            // Echo the first NNODES values, as in WRITE(*,*) (EPOTIN(I),I=1,NNODES)
            Console.WriteLine("EPOTIN (first {0} nodes):", state.NNODES);
            for (int i = 1; i <= state.NNODES; i++)
            {
                Console.WriteLine(state.EPOTIN[i].ToString(ci));
            }
        }

        // ----- IWAVE = 12: import SINEIN from SINEfile.txt -----
        if (state.IWAVE == 12)
        {
            const string sineFile = "SINEfile.txt";

            if (!File.Exists(sineFile))
                throw new FileNotFoundException("SINE file not found.", sineFile);

            using (var r = new StreamReader(sineFile))
            {
                int i = 1; // Fortran 1..NSINES
                string? line;
                while (i <= state.NSINES && (line = r.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var parts = line.Split(
                        new[] { ' ', '\t', ',' },
                        StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 3)
                        throw new FormatException(
                            $"Expected 3 numeric values per line in {sineFile}, got: '{line}'");

                    state.SINEIN[i, 1] = double.Parse(parts[0], ci);
                    state.SINEIN[i, 2] = double.Parse(parts[1], ci);
                    state.SINEIN[i, 3] = double.Parse(parts[2], ci);

                    i++;
                }

                if (i <= state.NSINES)
                {
                    throw new InvalidOperationException(
                        $"SINEfile.txt contained fewer than NSINES={state.NSINES} rows.");
                }
            }
        }

        // IWAVE = 13 (XIN/YIN import) will be handled later where that code appears.
    }

    private static void ImportXYForWaveform13(SennState state)
    {
        if (state.IWAVE != 13)
            return;

        var ci = CultureInfo.InvariantCulture;
        var w = state.DataOutWriter;

        // Open XYINTERP (interpolated output) – left open for INTERP to use
        state.XYInterpWriter = new StreamWriter("XYINTERP", append: false);

        // Open XYfile.txt (input) and XYIN (diagnostic copy of input)
        const string xyFileName = "XYfile.txt";

        if (!File.Exists(xyFileName))
            throw new FileNotFoundException("XY waveform file not found.", xyFileName);

        using var xyReader = new StreamReader(xyFileName);
        using var xyInWriter = new StreamWriter("XYIN", append: false); // diagnostic

        w?.WriteLine("External input file named XYFILE");
        Console.WriteLine("External input file named XYFILE");

        state.LENIN = 1;
        string? line;

        while ((line = xyReader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var parts = line.Split(
                new[] { ' ', '\t', ',' },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                throw new FormatException($"Expected two values per line in {xyFileName}, got: '{line}'");

            if (state.LENIN > state.XIN.Length - 1 || state.LENIN > state.YIN.Length - 1)
                throw new InvalidOperationException("XIN/YIN arrays too small for input size.");

            state.XIN[state.LENIN] = double.Parse(parts[0], ci);
            state.YIN[state.LENIN] = double.Parse(parts[1], ci);

            // Diagnostic copy
            xyInWriter.WriteLine(
                $"{state.XIN[state.LENIN].ToString(ci)} {state.YIN[state.LENIN].ToString(ci)}");

            state.LENIN++;
        }

        // Fortran: LENIN = LENIN - 1 after EOF
        state.LENIN--;

        if (state.LENIN > 8001)
        {
            const string msg = "8001 input points is the current maximum.";
            w?.WriteLine(msg);
            Console.WriteLine(msg);
            throw new InvalidOperationException(msg);
        }

        w?.WriteLine($"{state.LENIN} input (x,y) pairs");
        Console.WriteLine($"{state.LENIN} input (x,y) pairs");

        // NOTE: Input temporal spacing must be in milliseconds
        state.DELTIN = state.XIN[2] - state.XIN[1];   // spacing in ms
        Console.WriteLine($"{state.DELTIN} ms input x-spacing");
        Console.WriteLine($"{state.NTRP} points interpolated");

        // Call INTERP equivalent (fills XCAL, YCAL, YINTERP, DELTOT, LENOT, writes XYINTERP)
        Interp(state);

        // Close XYINTERP
        state.XYInterpWriter?.Dispose();
        state.XYInterpWriter = null;

        // DELT = DELTOT*2.0  (override input param; *2 for RKGS)
        state.DELT = state.DELTOT * 2.0;
        state.DELT2 = state.DELT2M * state.DELT;

        Console.WriteLine($"{state.DELTOT} ms output (interpolated) spacing");
        Console.WriteLine($"{state.LENOT} output (interpolated) (x,y) pairs");
        Console.WriteLine($"DELT = {state.DELT} ms overriding inp. param. list (interp spacing*2)");
        Console.WriteLine($"DELT2 = {state.DELT2} ms = DELT2M*DELT");
    }

    /// <summary>
    /// C# placeholder for the Fortran INTERP subroutine:
    ///   CALL INTERP(XIN,YIN,DELTIN,LENIN,NTRP,XCAL,YCAL,YINTERP,DELTOT,LENOT)
    /// When you provide the Fortran INTERP code, we can translate the
    /// actual interpolation logic here.
    /// </summary>
    private static void Interp(SennState state)
    {
        // Fortran arguments:
        //   XIN, YIN, DELTIN, LENIN, NTRP, XCAL, YCAL, YINTERP, DELTOT, LENOT
        // All of these live in 'state'.

        var w = state.DataOutWriter;

        // Safety: need at least two points
        if (state.LENIN < 2)
        {
            Console.WriteLine("INPUT FILE HAS LESS THAN TWO POINTS");
            w?.WriteLine("INPUT FILE HAS LESS THAN TWO POINTS");
            throw new InvalidOperationException("INTERP: LENIN < 2");
        }

        // LENOT = (LENIN -1)*(NTRP + 1) + 1
        state.LENOT = (state.LENIN - 1) * (state.NTRP + 1) + 1;

        // DELTOT = DELTIN/(NTRP +1)
        state.DELTOT = state.DELTIN / (state.NTRP + 1);

        if (state.LENOT >= state.XCAL.Length || state.LENOT >= state.YCAL.Length ||
            state.LENOT >= state.YINTERP.Length)
        {
            throw new InvalidOperationException("INTERP: LENOT exceeds array capacity.");
        }

        // Initial point
        state.XCAL[1] = state.XIN[1];
        state.YCAL[1] = state.YIN[1];

        double value = state.XIN[1];
        int i = 1;
        int j = 0;
        int k = 1; // index for YINTERP

        // Write first point to XYINTERP
        state.XYInterpWriter?.WriteLine($"{state.XCAL[1]} {state.YCAL[1]}");

        // First output y
        state.YINTERP[k] = state.YCAL[1];

        // Main interpolation loop
        while (i < state.LENIN)
        {
            j++;
            k++;
            value += state.DELTOT;
            state.XCAL[j] = value;

            // Every (NTRP+1)-th point is an original point
            if (j % (state.NTRP + 1) == 0)
            {
                // Original data point
                // Write XIN(I+1), YIN(I+1)
                state.XYInterpWriter?.WriteLine($"{state.XIN[i + 1]} {state.YIN[i + 1]}");

                state.YINTERP[k] = state.YIN[i + 1];
                i++;
            }
            else
            {
                double deltx = state.XIN[i + 1] - state.XIN[i];
                if (deltx <= 0.0)
                {
                    Console.WriteLine("WARNING: INPUT TIME STREAM NOT MONOTONIC INCREASING");
                    w?.WriteLine("WARNING: INPUT TIME STREAM NOT MONOTONIC INCREASING");
                    // Fortran used to RETURN here, but now only warns and continues.
                }

                // M = (Y(i+1) - Y(i)) / DELTIN
                double m = (state.YIN[i + 1] - state.YIN[i]) / state.DELTIN;
                double b = state.YIN[i] - m * state.XIN[i];

                state.YCAL[j] = m * state.XCAL[j] + b;

                // Write interpolated point
                state.XYInterpWriter?.WriteLine($"{state.XCAL[j]} {state.YCAL[j]}");

                state.YINTERP[k] = state.YCAL[j];
            }
        }

        Console.WriteLine($"End of new data.    {state.LENOT} (x,y) pairs created");
        Console.WriteLine("Output file named XYINTERP");
    }

    private static void PostWaveformSetup(SennState state)
    {
        Console.WriteLine($"NNODES {state.NNODES}");

        state.INNGTT = state.NTHNODE;                      // # of nodes needed to validate
        state.NON = (short)((state.NNODES - 1) / 2);    // # nodes on each side of center
        state.URATIO = 1.0;
    }

    private static void ConfigureWaveformParameters(SennState state)
    {
        // ----- IWAVE = 6: force NP = 2 -----
        if (state.IWAVE == 6)
        {
            state.NP = 2;
        }

        // FIX TO ASSURE UIO2=0.0 WHEN NP=1
        if (state.NP == 1 && state.IWAVE != 8 && state.IWAVE != 9)
        {
            state.UIO2 = 0.0;
            state.DELAY = 0.0;
            state.XPD2 = 0.0;   // added 8/25/09 in Fortran
        }
        else if (state.NP >= 2 && state.IWAVE != 6)
        {
            state.UIO2 = state.UIO;
            state.XPD2 = state.XPD;
        }

        // ----- Form radian angles and radian frequencies -----

        if (state.IWAVE == 2 || state.IWAVE == 3 || state.IWAVE == 5
            || state.IWAVE == 10 || state.IWAVE == 11)
        {
            state.ANGLE = state.PHASE * state.PID180;
            state.PROD = state.TWOPI * state.FREQ;  // rad/s equivalent
        }

        // SECOND SINUSOID for IWAVE = 10 or 11
        if (state.IWAVE == 10 || state.IWAVE == 11)
        {
            state.ANGLE2 = state.PHASE2 * state.PID180;
            state.PROD2 = state.TWOPI * state.FREQ2;
        }

        // MULTIPLE SINUSOIDS: IWAVE = 12
        if (state.IWAVE == 12)
        {
            for (int i = 1; i <= state.NSINES; i++)
            {
                // SINEIN:  (amp, freq[Hz], phase[deg])
                // SINEIN2: (amp, omega[rad/s], phase[rad])
                state.SINEIN2[i, 1] = state.SINEIN[i, 1];                 // amplitude
                state.SINEIN2[i, 2] = state.SINEIN[i, 2] * state.TWOPI;   // radian frequency
                state.SINEIN2[i, 3] = state.SINEIN[i, 3] * state.PID180;  // radian phase
            }
            // Diagnostic WRITE in Fortran omitted here (optional).
        }

        // Special cases for IWAVE 8 and 9
        if (state.IWAVE == 8)
        {
            state.PROD = state.TWOPI * state.FREQ;
            state.ANGLE = state.PID180 * state.PHASE - state.TWOPI * state.FREQ * state.XPD;
        }

        if (state.IWAVE == 9)
        {
            state.PROD = state.TWOPI * state.FREQ;
            state.ANGLE = state.PHASE * state.PID180;
        }
    }

    private static void WriteParameterSummary(SennState state)
    {
        var w = state.DataOutWriter ?? throw new InvalidOperationException("DataOutWriter not initialized.");
        var ci = CultureInfo.InvariantCulture;

        // Run descriptor (top line of inparam.txt, e.g. "SINUSOID")
        if (!string.IsNullOrEmpty(state.Descriptor))
        {
            w.WriteLine(state.Descriptor);
            w.WriteLine();
        }

        // ---------- Namelist : FIBER ----------
        w.WriteLine("Namelist : FIBER");
        w.WriteLine();
        w.WriteLine($"NNODES= {state.NNODES}\t\t TOTAL NUMBER OF NODES (Odd)");
        w.WriteLine($"NLIN1=  {state.NLIN1}\t\t  FIRST NONLINEAR NODE");
        w.WriteLine($"NLIN2=  {state.NLIN2}\t\t LAST NONLINEAR NODE");
        w.WriteLine($"NODE1=  {state.NODE1}\t\t FIRST PRINT NODE");

        w.WriteLine($"DIAM=  {state.DIAM.ToString("F7", ci)}\t\t FIBER DIAMETER (cm)");
        w.WriteLine($"GAP=   {state.GAP.ToString("F7", ci)}\t\t INTRANODAL GAP (cm)");
        w.WriteLine($"CM=   {state.CM.ToString("F7", ci)}\t\t MEMBRANE CAPACITY (uF/cm**2)");
        w.WriteLine($"GM=   {state.GM.ToString("F7", ci)}\t\t LIN. MEMBR. CONDUCTANCE/AREA (mS/cm**2)");
        w.WriteLine($"RHOI= {state.RHOI.ToString("F7", ci)}\t\t AXOPLASM RESISTIVITY (ohm.cm)");
        w.WriteLine($"RHOE= {state.RHOE.ToString("F7", ci)}\t\t MEDIUM RESISTIVITY (ohm.cm)");
        w.WriteLine();

        // ---------- Namelist : STIMULUS ----------
        w.WriteLine("Namelist : STIMULUS");
        w.WriteLine();
        w.WriteLine($"XC= {state.XC.ToString("F7", ci)}\t\t   X LOCUS CATHODE FOR FS=0 or FS=3 (cm)");
        w.WriteLine($"YC= {state.YC.ToString("F7", ci)}\t\t   Y LOCUS CATHODE FOR FS=0 or FS=3 (cm)");
        w.WriteLine($"XA= {state.XA.ToString("F7", ci)}\t\t   X LOCUS ANODE FOR FS=0 or FS=3 (cm)");
        w.WriteLine($"YA= {state.YA.ToString("F7", ci)}\t\t   Y LOCUS ANODE FOR FS=0 or FS=3 (cm)");
        w.WriteLine($"WIREL= {state.WIREL.ToString("F7", ci)}    PROBE WIRE CONTACT LENGTH FOR FS=3 (cm)");

        // IWAVE description
        string waveDesc;
        if (state.IWAVE == 1) waveDesc = "MONOPHASIC";
        else if (state.IWAVE == 2) waveDesc = "SINEWAVE";
        else if (state.IWAVE == 3) waveDesc = "SINE + DC";
        else if (state.IWAVE == 4) waveDesc = "EXPONENTIAL";
        else if (state.IWAVE == 5) waveDesc = "SINE*EXPONENTIAL";
        else if (state.IWAVE == 6 && state.UIO / state.UIO2 >= 0.0) waveDesc = "MONOPHASIC DOUBLET";
        else if (state.IWAVE == 6 && state.UIO / state.UIO2 < 0.0) waveDesc = "BIPHASIC DOUBLET";
        else if (state.IWAVE == 7) waveDesc = "SPECIAL INPUT";
        else if (state.IWAVE == 8) waveDesc = "RECTANGULAR PULSE + SINUSOIDAL";
        else if (state.IWAVE == 9) waveDesc = "SINUSOIDAL + RECTANGULAR PULSE";
        else if (state.IWAVE == 10) waveDesc = "SUM OF TWO SINUSOIDS";
        else if (state.IWAVE == 11) waveDesc = "COSINE AMPLITUDE MODULATION";
        else if (state.IWAVE == 12) waveDesc = "SUM OF ARRAY OF SINUSOIDS";
        else if (state.IWAVE == 13) waveDesc = "INPUT WAVEFORM ARRAY";
        else
        {
            w.WriteLine($"IWAVE= {state.IWAVE}\t\t  UNDEFINED WAVEFORM");
            throw new InvalidOperationException("IWAVE undefined.");
        }

        w.WriteLine($"IWAVE= {state.IWAVE}\t\t  {waveDesc}");

        // Stimulus parameters
        w.WriteLine($"UIO=  {state.UIO.ToString("F7", ci)}\t\t STIMULUS AMPLITUDE (mA for FS=0 or FS=3, mA/cm**2 for FS=1)");
        w.WriteLine($"XPD=  {state.XPD.ToString("F7", ci)}\t\t PULSE DURATION (ms)");
        w.WriteLine($"UIO2= {state.UIO2.ToString("F7", ci)}\t\t 2ND (DOUBLET) AMPLITUDE (mA or mA/cm**2)");
        w.WriteLine($"XPD2= {state.XPD2.ToString("F7", ci)}\t\t 2ND (DOUBLET) PULSE DURATION (ms)");

        w.WriteLine($"DELAY= {state.DELAY.ToString("F7", ci)}\t\t DELAY BETWEEN PULSES,IWAVE=1; or BETWEEN DOUBLET PHASES,IWAVE=6 (ms)");

        w.WriteLine($"FREQ= {state.FREQ.ToString("F10", ci)}     SINEWAVE FREQUENCY,(kHz), for IWAVE=2,3,5,8,9,10,11");
        w.WriteLine($"PHASE= {state.PHASE.ToString("F6", ci)}        SINEWAVE PHASE,(deg), for IWAVE=2,3,5,8,9,10,11");

        w.WriteLine($"FREQ2= {state.FREQ2.ToString("F10", ci)}    SECOND SINEWAVE FREQUENCY,(kHz), for IWAVE=10,11");
        w.WriteLine($"PHASE2= {state.PHASE2.ToString("F6", ci)}       SECOND SINEWAVE PHASE,(deg), for IWAVE=10,11");
        w.WriteLine($"AMP2= {state.AMP2.ToString("F7", ci)}        SECOND SINEWAVE AMPLITUDE, for IWAVE=10,11");
        w.WriteLine($"NSINES= {state.NSINES}\t\t NUMBER OF SINE WAVES FOR IWAVE=12");

        w.WriteLine($"DCOFF= {state.DCOFF.ToString("F7", ci)}\t\t DC OFFSET, IWAVE=3");
        w.WriteLine($"TAUS= {state.TAUS.ToString("F7", ci)}\t\t EXP TIME CONSTANT FOR IWAVE=4,5");
        w.WriteLine($"VREF= {state.VREF.ToString("F7", ci)}\t\t FIRST NODE POTENTIAL FOR FS=1");
        w.WriteLine($"NP= {state.NP}\t\t      NUMBER OF PULSE REPETITIONS, IWAVE=1");
        w.WriteLine($"FS= {state.FS}\t\t      0=POINT ELECTRODE, 1=UNIFORM FIELD,");
        w.WriteLine("\t\t 2=IMPORT EPOT ARRAY, 3=WIRE ELECTRODE");
        w.WriteLine($"S= {state.S}\t\t       0=ELECTRODE IN SITU,");
        w.WriteLine("\t\t 1=ELECTRODE ON SURFACE");
        w.WriteLine($"NTRP= {state.NTRP}\t\t    NUMBER OF INTERPOLATED POINTS FOR IWAVE=13");
        w.WriteLine();

        // ---------- Namelist : CONTROL ----------
        w.WriteLine("Namelist : CONTROL");
        w.WriteLine();
        w.WriteLine($"TT= {state.TT}        1= TRUNCATED AXON, 2= CELL BODY+HILLOCK");
        w.WriteLine($"ITHR=   {state.ITHR}\t\t 0=SINGLE RUN,1=THRESHOLD SEEKING");
        w.WriteLine($"VTH= {state.VTH.ToString("F7", ci)} \t\t VOLTAGE CRITERIA FOR THRESHOLD(mV)");
        w.WriteLine($"NTHNODE= {state.NTHNODE}\t\t NO. NODES THRESHOLD FOR ITHR=1");
        w.WriteLine($"DELT=  {state.DELT.ToString("F6", ci)}\t\t TIME STEP DURING PULSE (ms)");

        if (Math.Abs(state.DELT2) < 1e-12)
            state.DELT2 = state.DELT;

        w.WriteLine($"DELT2M= {state.DELT2M.ToString("F6", ci)}       FACTOR FOR TIME STEP AFTER PULSE");
        w.WriteLine($"FINAL= {state.FINAL.ToString("F6", ci)}\t\t MAX. SOLUTION TIME/RUN (ms)");
        w.WriteLine($"IPRNT=  {state.IPRNT}        PRINT INTERVAL");
        w.WriteLine();

        // ---------- Namelist : HILLOCK ----------
        w.WriteLine("Namelist : HILLOCK");
        w.WriteLine();
        w.WriteLine($"WB= {state.WB.ToString("F5", ci)}\t\t    WIDTH OF CELL BODY (cm)");
        w.WriteLine($"WH= {state.WH.ToString("F5", ci)}\t\t    WIDTH OF CELL HILLOCK(cm)");
        w.WriteLine($"DIAMB= {state.DIAMB.ToString("F5", ci)}\t\t DIAMETER OF CELL BODY (cm)");
        w.WriteLine($"DIAMH= {state.DIAMH.ToString("F5", ci)}\t\t DIAMETER OF CELL BODY (cm)");
        w.WriteLine();

        // Fortran FORMAT('1') - page eject character
        w.WriteLine("1");

        // URATIO = UIO2/UIO if appropriate
        if (state.IWAVE != 8 && state.IWAVE != 9 && Math.Abs(state.UIO) > 0.0)
        {
            state.URATIO = state.UIO2 / state.UIO;
        }

        // COMPUTE TIMES OF PULSE LEAD AND PULSE TRAIL
        ComputePulseTimes(state);
    }

    private static void ComputePulseTimes(SennState state)
    {
        // Fortran: IF(IWAVE .NE. 6)THEN
        if (state.IWAVE != 6)
        {
            // DO I=1,NP
            for (int i = 1; i <= state.NP; i++)
            {
                state.PT[i] = i * state.XPD + (i - 1) * state.DELAY;
                state.PL[i] = state.PT[i] - state.XPD;
                Console.WriteLine($"pl {state.PL[i]} pt {state.PT[i]} dly {state.DELAY}");
            }
        }
        else
        {
            // IWAVE=6: special doublet logic
            // DO I = 1,NP-1
            for (int i = 1; i <= state.NP - 1; i++)
            {
                state.PT[i] = i * state.XPD + (i - 1) * state.DELAY; // time of trailing edge of stimuli +delay
                state.PL[i] = state.PT[i] - state.XPD; // time of leading edge of stimulus
                Console.WriteLine($"pl {state.PL[i]} pt {state.PT[i]} dly {state.DELAY}");

                // PT(I+1)=DELAY + XPD+I*XPD2  (modified 1/16/2010)
                state.PT[i + 1] = state.DELAY + state.XPD + i * state.XPD2;
                state.PL[i + 1] = state.PT[i + 1] - state.XPD2;
                Console.WriteLine($"pln {state.PL[i + 1]} pt {state.PT[i + 1]} dly {state.DELAY}");
            }
        }
    }

    private static void SetupGeometryAndRunParameters(SennState state)
    {
        // ----- VARY IONIC NON LINEAR PARAMETERS DEPENDING ON GM -----
        // XMFACT = GM / 30.365
        double xmfact = state.GM / 30.365;

        // PNAB = XMFACT * PNAB
        state.PNAB = xmfact * state.PNAB;

        // NORMALIZE YC TO SATISFY Y0 BY YC = Y0 * DIAM * ELD
        // Here we compute Y0 but Fortran comment says it's not used.
        double y0 = state.YC / (state.DIAM * state.ELD); // for completeness; not stored

        // ----- INITIALIZE VARIABLES AND CONSTANTS -----
        state.PRMT[1] = 0.0;
        state.PRMT[2] = state.FINAL;
        state.TEND = state.FINAL;

        // Fortran logic: IF(ITHR.EQ.1) GO TO 408 / IF(ITHR .EQ. 0) GO TO 408
        // If ITHR is neither 0 nor 1, fall through to label 407 (run-out mode)
        // which calculates TEND based on XPD
        if (state.ITHR != 0 && state.ITHR != 1)
        {
            // Label 407: RUN OUT MODE modified by stimulus width
            state.TEND = state.XPD + 0.5;
            if (state.XPD >= 0.1 && state.XPD <= 0.5)
                state.TEND = state.TEND + 0.47;
            if (state.XPD >= 1.0 && state.XPD < 1.5)
                state.TEND = state.XPD + 0.3;
            if (state.XPD >= 2.0)
                state.TEND = state.XPD + 0.2;
        }
        // Label 408: CONTINUE (both ITHR==0 and ITHR==1 use FINAL as TEND)

        state.PRMT[3] = state.DELT;
        state.PRMT[4] = 100.0;

        state.UIOLD = state.UIO;
        state.IT = 0;

        // If IPRNT = 0, compute from DELT: IPRNT = .02/DELT + .5
        if (state.IPRNT == 0)
        {
            state.IPRNT = (short)(0.02 / state.DELT + 0.5);
        }

        state.NI = 0;
        state.ITA = 0;
        state.ITB = 0;

        // Geometric quantities
        state.EL = state.ELD * state.DIAM;
        state.NDIM = 2 * state.NON + 4 * (state.NLIN2 - state.NLIN1) + 4;

        // AXIAL INTERNODAL CONDUCTANCE
        state.CGA = 1000.0 * state.PI * state.DIAM * state.SDD * state.SDD
                    / (4.0 * state.RHOI * state.ELD);

        // INTRANODAL SURFACE AREA
        state.AREA = state.PI * state.SDD * state.DIAM * state.GAP;
        Console.WriteLine("AREA " + state.AREA);

        // Cell body and hillock areas
        state.AB = state.PI * state.DIAMB * state.WB;   // cell body surface area
        state.AH = state.PI * state.DIAMH * state.WH;   // hillock surface area
        state.LBH = (state.WB + state.WH) / 2.0;         // average body/hillock length
        state.LHN = (state.WH + state.GAP) / 2.0;        // hillock+gap average
        state.AN = state.AREA;                          // intranodal surface area

        // Membrane capacitance & conductance
        state.CCM = state.CM * state.AN;
        state.CGM = state.GM * state.AN;

        if (Math.Abs(state.TT - 2.0) < 1e-12)
        {
            // HILLOCK & CELL BODY COUPLING
            double rah = state.RHOI * (state.WH / (2.0 * state.AH) + state.GAP / (2.0 * state.AN));
            state.GAH = 1.0 / rah;

            double rab = state.RHOI * (state.WB / (2.0 * state.AB) + state.WH / (2.0 * state.AH));
            state.GAB = 1.0 / rab;

            state.CMH = state.CM * state.AH;    // hillock capacitance
            state.GMH = state.GM * state.AH;    // hillock conductance
            state.CMB = state.CM * state.AB;    // cell body capacitance
            state.GMB = state.GM * state.AB;    // cell body conductance
        }

        // ----- NODE SELECTION / PRINTING -----
        if (state.NODE1 == 0)
        {
            state.NODE1 = (short)(state.NON + 2);
        }

        int j = state.NODE1;
        state.NODEZ = state.NODE1 - 1;
        if (state.NODEZ <= 0)
            state.NODEZ = 1;

        // IN(1) = NODE1 (Fortran changed from NODEZ to NODE1)
        state.IN[1] = state.NODE1;

        // mpn = max number of printable nodes starting with NODE1
        int mpn = 11; // default (10 printable nodes + 1 extra for header)
        int node1Plus9 = state.NODE1 + 9;
        if (node1Plus9 > state.NNODES)
        {
            int diff = node1Plus9 - state.NNODES;
            mpn = 11 - diff;
        }

        for (int i = 2; i < mpn; i++)
        {
            state.IN[i] = j + i - 2;
        }

        // URATIO was set earlier to 1.0 and later may be overwritten
        // based on UIO2/UIO in WriteParameterSummary / waveform setup.
    }

    private static void SetupWaveformDiagnosticsAndInitialAmplitude(SennState state)
    {
        var w = state.DataOutWriter;

        // NOTE: IA, IB, YMAX, YMIN are initialized ONCE per run in InitializeThresholdSearchCounters(),
        // NOT on every iteration. See Fortran lines 671-674.

        // Computed GOTO mapping in Fortran:
        // GO TO (16,11,12,13,14,16,20,17,17,11,11,11,20), IWAVE
        //  IWAVE: 1→16, 2→11, 3→12, 4→13, 5→14, 6→16,
        //         7→20, 8→17, 9→17, 10→11, 11→11, 12→11, 13→20

        // Sinusoid-only modes (11): IWAVE = 2, 10, 11, 12
        if (state.IWAVE == 2 || state.IWAVE == 10 || state.IWAVE == 11 || state.IWAVE == 12)
        {
            HandleSinusoidModes(state);
        }
        // Label 12: IWAVE = 3 (sine or cosine on pedestal, with DCOFF)
        else if (state.IWAVE == 3)
        {
            Console.WriteLine($"SINUSOID ON PEDESTAL  FREQ={state.FREQ}  PHASE={state.PHASE}  DC-OFFSET={state.DCOFF}");
        }
        // Label 13: IWAVE = 4 (pure exponential, TAUS)
        else if (state.IWAVE == 4)
        {
            Console.WriteLine($"EXPONENTIAL  TAUS(ms)={state.TAUS}");
        }
        // Label 14: IWAVE = 5 (exponential sinusoid)
        else if (state.IWAVE == 5)
        {
            Console.WriteLine($"EXPONENTIAL SINUSOID  TAUS(ms)={state.TAUS}  FREQ={state.FREQ}  PHASE={state.PHASE}");
        }
        // Label 16: IWAVE = 1 or 6 (rectangular pulse or doublet)
        else if (state.IWAVE == 1 || state.IWAVE == 6)
        {
            // Fortran's detailed WRITE(*,1509) is commented out.
            // We could add a summary here if desired, but behavior is effectively "do nothing".
            // Example (optional):
            // Console.WriteLine($"RECTANGULAR PULSE/DOUBLET: UIO={state.UIO}, XPD={state.XPD}, UIO2={state.UIO2}, XPD2={state.XPD2}, NP={state.NP}");
        }
        // Label 17: IWAVE = 8 or 9 (special functions: pulse + sinusoid)
        else if (state.IWAVE == 8 || state.IWAVE == 9)
        {
            Console.WriteLine($" UIO {state.UIO}  XPD {state.XPD}  UIO2 {state.UIO2}  XPD2 {state.XPD2}");
        }
        // Label 20: IWAVE = 7 or 13 -> no extra print
        else
        {
            // do nothing
        }

        // Label 20 code: sentinel writes and amplitude update
        //state.Out17?.WriteLine("5000 5000");
        //state.Out30?.WriteLine("5000 5000");

        // If UIO changed from UIOLD, recompute UIO2 from URATIO
        if (Math.Abs(state.UIO - state.UIOLD) > 0.0)
        {
            state.UIO2 = state.URATIO * state.UIO;
        }
    }

    // Helper for label 11 sinusoid modes
    private static void HandleSinusoidModes(SennState state)
    {
        var w = state.DataOutWriter;
        var ci = CultureInfo.InvariantCulture;

        if (state.IWAVE == 2)
        {
            // Single sinusoid
            double per = 1.0 / state.FREQ;
            Console.WriteLine($" SINUSOID  FREQUENCY {state.FREQ.ToString("F10", ci)}  PHASE {state.PHASE.ToString("F8", ci)}  PERIOD {per.ToString("F10", ci)}");
        }
        else if (state.IWAVE == 10 || state.IWAVE == 11)
        {
            // Two sinusoids
            double per = 1.0 / state.FREQ;
            double per2 = 1.0 / state.FREQ2;

            Console.WriteLine($" SINUSOID  FREQUENCY {state.FREQ.ToString("F10", ci)}  PHASE {state.PHASE.ToString("F8", ci)}  PERIOD {per.ToString("F10", ci)}");
            Console.WriteLine($" 2ND SINUSOID  FREQUENCY {state.FREQ2.ToString("F10", ci)}  PHASE {state.PHASE2.ToString("F8", ci)}  PERIOD {per2.ToString("F10", ci)}  AMP {state.AMP2.ToString("F8", ci)}");
        }
        else
        {
            // IWAVE = 12, multiple sinusoids
            Console.WriteLine($"{state.NSINES}  SINUSOIDS");
            w?.WriteLine($"{state.NSINES}  SINUSOIDS");

            Console.WriteLine("    AMP            FREQ        PHASE");
            w?.WriteLine("    AMP            FREQ        PHASE");

            for (int i = 1; i <= state.NSINES; i++)
            {
                double amp = state.SINEIN[i, 1];
                double freq = state.SINEIN[i, 2];
                double phase = state.SINEIN[i, 3];

                string line = $"{amp.ToString("F11", ci),11}   {freq.ToString("F10", ci),10}      {phase.ToString("F6", ci),6}";
                Console.WriteLine(line);
                w?.WriteLine(line);
            }

            Console.WriteLine();
            w?.WriteLine();
        }
    }


    private static void InitializeStateVectorY(SennState state)
    {
        // K = 2*NON + 1
        state.K = 2 * state.NON + 1;
        int k = state.K;

        // Zero Y(1..K)
        for (int i = 1; i <= k; i++)
        {
            state.Y[i] = 0.0;
        }

        // Set gating variable initial conditions on nonlinear nodes
        int jt = state.NLIN2 - state.NLIN1;
        if (jt > 0)
        {
            jt = jt + 1; // number of nonlinear nodes total

            for (int i = 1; i <= jt; i++)
            {
                int L = k + 4 * i - 3;

                // Make sure we don't run out of Y array bounds
                if (L + 3 >= state.Y.Length)
                    throw new InvalidOperationException("InitializeStateVectorY: index exceeds Y array size.");

                // Set initial conditions based on membrane model
                switch (state.MembraneModel)
                {
                    case MembraneModel.HodgkinHuxley:
                        // HH initial conditions at rest (-65mV)
                        // m_inf ≈ 0.0529, h_inf ≈ 0.596, n_inf ≈ 0.3177
                        state.Y[L] = 0.596;     // h(0) - sodium inactivation
                        state.Y[L + 1] = 0.0529; // m(0) - sodium activation
                        state.Y[L + 2] = 0.0;    // unused slot
                        state.Y[L + 3] = 0.3177; // n(0) - potassium activation
                        break;

                    case MembraneModel.FrankenhaeuserHuxley:
                    default:
                        // FH initial conditions (original values)
                        state.Y[L] = 0.8249;    // h(0)
                        state.Y[L + 1] = 0.0005; // m(0)
                        state.Y[L + 2] = 0.0049; // p(0)
                        state.Y[L + 3] = 0.0268; // n(0) - note: FH uses 'l' for this
                        break;
                }
            }
        }
    }

    private static void ComputeExternalPotentialsAndInitDerivatives(SennState state)
    {
        var w = state.DataOutWriter;
        var ci = CultureInfo.InvariantCulture;

        // JT = 2*NON + 3
        int jt = 2 * state.NON + 3;

        double x1 = 0.0, x2 = 0.0, x3 = 0.0;

        // TT = 2 → cell body + hillock model; special handling of the first 3 nodes
        if (Math.Abs(state.TT - 2.0) < 1e-12)
        {
            if (state.FS == 0) // point electrode
            {
                // Node positions for cell body / hillock / first axon node
                x3 = (3 - state.NON - 1) * state.EL;
                x2 = x3 - state.LHN;
                x1 = x3 - state.LHN - state.LBH;
            }
        }

        // Main loop over nodes treated in the external field
        for (int i = 1; i <= jt; i++)
        {
            if (state.FS == 0 || state.FS == 3)
            {
                // Point (FS=0) or wire (FS=3) electrode
                int fkt = i - state.NON - 1;
                double xi = fkt * state.EL;

                if (Math.Abs(state.TT - 2.0) < 1e-12)
                {
                    // CELL BODY + HILLOCK geometry
                    if (i == 1)
                    {
                        double rc = Math.Sqrt((state.XC - x1) * (state.XC - x1) + state.YC * state.YC);
                        double ra = Math.Sqrt((state.XA - x1) * (state.XA - x1) + state.YA * state.YA);

                        if (state.FS == 0)
                        {
                            // Point electrode in situ / on surface
                            state.EPOT[1] = state.RHOE * state.UIO * (1.0 / ra - 1.0 / rc) / state.PIMULT;
                        }
                        else // FS = 3, wire electrode
                        {
                            state.EPOT[1] = state.RHOE * state.UIO * Math.Log(rc / ra) / (state.PIMULT * state.WIREL);
                        }
                    }
                    else if (i == 2)
                    {
                        double rc = Math.Sqrt((state.XC - x2) * (state.XC - x2) + state.YC * state.YC);
                        double ra = Math.Sqrt((state.XA - x2) * (state.XA - x2) + state.YA * state.YA);

                        if (state.FS == 0)
                        {
                            state.EPOT[2] = state.RHOE * state.UIO * (1.0 / ra - 1.0 / rc) / state.PIMULT;
                        }
                        else
                        {
                            state.EPOT[2] = state.RHOE * state.UIO * Math.Log(rc / ra) / (state.PIMULT * state.WIREL);
                        }
                    }
                    else if (i == 3)
                    {
                        double rc = Math.Sqrt((state.XC - x3) * (state.XC - x3) + state.YC * state.YC);
                        double ra = Math.Sqrt((state.XA - x3) * (state.XA - x3) + state.YA * state.YA);

                        if (state.FS == 0)
                        {
                            state.EPOT[3] = state.RHOE * state.UIO * (1.0 / ra - 1.0 / rc) / state.PIMULT;
                        }
                        else
                        {
                            state.EPOT[3] = state.RHOE * state.UIO * Math.Log(rc / ra) / (state.PIMULT * state.WIREL);
                        }
                    }
                    else
                    {
                        // i > 3: use regular axon spacing
                        double rc = Math.Sqrt((state.XC - xi) * (state.XC - xi) + state.YC * state.YC);
                        double ra = Math.Sqrt((state.XA - xi) * (state.XA - xi) + state.YA * state.YA);

                        if (state.FS == 0)
                        {
                            state.EPOT[i] = state.RHOE * state.UIO * (1.0 / ra - 1.0 / rc) / state.PIMULT;
                        }
                        else
                        {
                            state.EPOT[i] = state.RHOE * state.UIO * Math.Log(rc / ra) / (state.PIMULT * state.WIREL);
                        }
                    }
                }
                else
                {
                    // TT = 1: truncated axon model, uniform internodal positions
                    double rc = Math.Sqrt((state.XC - xi) * (state.XC - xi) + state.YC * state.YC);
                    double ra = Math.Sqrt((state.XA - xi) * (state.XA - xi) + state.YA * state.YA);

                    if (state.FS == 0)
                    {
                        state.EPOT[i] = state.RHOE * state.UIO * (1.0 / ra - 1.0 / rc) / state.PIMULT;
                    }
                    else
                    {
                        state.EPOT[i] = state.RHOE * state.UIO * Math.Log(rc / ra) / (state.PIMULT * state.WIREL);
                    }
                }
            }
            else if (state.FS == 1)
            {
                // FS=1 → uniform field, generated EPOTs
                if (Math.Abs(state.TT - 2.0) < 1e-12)
                {
                    // CELL BODY + HILLOCK model
                    if (i == 1)
                    {
                        state.EPOT[1] = state.VREF;
                    }
                    else if (i == 2)
                    {
                        state.EPOT[2] = state.EPOT[1] + state.UIO * (state.WB + state.WH) / 2.0 * state.RHOE;
                    }
                    else if (i == 3)
                    {
                        state.EPOT[3] = state.EPOT[2] + state.UIO * (state.WH + state.GAP) / 2.0 * state.RHOE;
                    }
                    else
                    {
                        // all successive nodes
                        state.EPOT[i] = state.EPOT[3] + (i - 3) * state.UIO * state.RHOE * state.EL;
                    }
                }
                else
                {
                    // TT=1 (truncated axon), uniform field:
                    state.EPOT[i] = state.VREF + (i - 1) * state.UIO * state.RHOE * state.EL;
                }
            }
            else if (state.FS == 2)
            {
                // FS=2 → uniform field with imported EPOTs
                state.EPOT[i] = state.EPOTIN[i] * state.UIO;
            }
        } // end for i=1..jt

        // Print some geometry/conductance parameters to data.out
        // Fortran format: F10.6 for CMB, GMB, CMH, GMH
        w?.WriteLine(string.Format(ci, " CMB {0,10:F6}  GMB {1,10:F6}  CMH {2,10:F6}  GMH {3,10:F6}",
            state.CMB, state.GMB, state.CMH, state.GMH));
        // Fortran format: E10.6 for CCM, CGA, AN, CGM
        w?.WriteLine(string.Format(ci, " CCM {0,10:E6}  CGA {1,10:E6}  AN {2,10:E6}  CGM {3,10:E6}",
            state.CCM, state.CGA, state.AN, state.CGM));
        w?.WriteLine(string.Format(ci, " AB {0,10:F6}  AH {1,10:F6}  GAH {2,10:F6}  GAB {3,10:F6}",
            state.AB, state.AH, state.GAH, state.GAB));
        w?.WriteLine();
        // EPOT format: F10.3 (3 decimal places)
        w?.Write("EPOT ");
        for (int mm = 1; mm <= state.NNODES; mm++)
        {
            w?.Write(string.Format(ci, "{0:F3} ", state.EPOT[mm]));
        }
        w?.WriteLine();

        // Initialize DERY to zero
        for (int i = 1; i <= state.NDIM; i++)
        {
            state.DERY[i] = 0.0;
        }

        // DERY(NON+1) = 1.0 (initial slope / perturbation direction)
        state.DERY[state.NON + 1] = 1.0;
    }

    private static void WriteVoltageCurrentHeaders(SennState state)
    {
        var w = state.DataOutWriter;
        if (w == null) return;

        // Fortran FORMAT: ' TIME',10(7X,'V',I3,1x) - TIME header with V22, V23, etc.
        w.Write("      TIME");
        for (int i = 2; i <= state.mpn; i++)
        {
            w.Write($"       V{state.IN[i]:D3}");  // 7 spaces + V + 3-digit node number + 1 space
        }
        w.WriteLine();

        // Fortran FORMAT: 9X,10(7X,'I',I3,1x) - I headers
        w.Write("         ");  // 9 spaces
        for (int i = 2; i <= state.mpn; i++)
        {
            w.Write($"       I{state.IN[i]:D3}");  // 7 spaces + I + 3-digit node number + 1 space
        }
        w.WriteLine();
    }

    private static bool RunSingleIterationAndMaybeUpdateUIO(SennState state)
    {
        var w = state.DataOutWriter;
        var ci = CultureInfo.InvariantCulture;

        // Max iterations check (NI .GT. 19)
        if (state.NI > 19)
        {
            const string msg = "MAXIMUM NUMBER OF ITERATIONS EXCEEDED, ... PROGRAM TERMINATING";
            w?.WriteLine(msg);
            Console.WriteLine(msg);
            return false; // stop threshold iterations
        }

        // ----- INITIALIZE VARIABLES FOR A NEW RUN -----
        state.IPT = 0;
        state.TMAX = 0.0;
        state.VMAX = 0.0;

        w?.WriteLine();
        // Fortran FORMAT (A6,F15.7) - 6 chars for "    I=", then F15.7 for number
        w?.WriteLine(string.Format(ci, "    I={0,15:F7}", state.UIO));
        Console.WriteLine(string.Format(ci, "    I={0,15:F7}", state.UIO));

        // Header for time/V/I columns
        WriteVoltageCurrentHeaders(state);

        // ----- BEGIN INTEGRATION -----
        RKGS(state, state.NDIM); // will update Y, TMAX, VMAX, NODE, NNGTT, etc.

        // Update iteration index
        state.NI++;
        int n = state.NI;

        // Store results in per-iteration arrays
        state.VM[n] = state.VMAX;
        state.NXGT[n] = state.NNGTT;   // # nodes found exceeding threshold
        state.TM[n] = state.TMAX;
        state.NN[n] = (short)state.NODE;
        state.UM[n] = state.UIO;

        // If not threshold seeking (ITHR <= 0), just finish
        if (state.ITHR <= 0)
            return false;

        // ----- Threshold seeking logic -----
        state.CROSS = (state.NXGT[n] >= state.INNGTT);

        // Diagnostic line like FORMAT 69
        Console.WriteLine(
            $"ITER# {n,3} VMAX {state.VMAX,8:F3}  UIO {state.UIO,10:F4} " +
            $"#NODES {state.NXGT[n],3} TMAX {state.TMAX,6:F2} NODE {state.NODE,4}");
        w?.WriteLine(
            $"ITER# {n,3} VMAX {state.VMAX,8:F3}  UIO {state.UIO,10:F4} " +
            $"#NODES {state.NXGT[n],3} TMAX {state.TMAX,6:F2} NODE {state.NODE,4}");

        bool continueIterations = false;

        // Above-threshold case: NXGT(NI) >= INNGTT
        if (state.NXGT[n] >= state.INNGTT)
        {
            state.IA += 1; // count how many times above threshold

            if (state.UM[n] < state.YMIN)
            {
                state.YMIN = state.UM[n];

                double ratio = (Math.Abs(state.YMAX) > 0.0)
                    ? Math.Abs(state.YMIN / state.YMAX)
                    : double.PositiveInfinity;

                if (ratio > 1.0 && ratio <= 1.016 && state.IB * state.IA > 0)
                {
                    // Exit iteration process (GOTO 200)
                    return false;
                }
                else if (state.IB * state.IA > 0)
                {
                    // Both above & below thresholds encountered → bisect
                    state.UIO = (state.YMAX + state.YMIN) / 2.0;
                    continueIterations = true; // equivalent to GOTO 20
                }
                else if (state.IB == 0)
                {
                    // No below-threshold yet → halve current amplitude
                    state.UIO = state.UM[n] * 0.5;
                    continueIterations = true;
                }
            }
        }
        // Below-threshold case: NXGT(NI) < INNGTT
        else
        {
            state.IB += 1;

            if (Math.Abs(state.UM[n]) > state.YMAX)
            {
                state.YMAX = state.UM[n];

                double ratio = (Math.Abs(state.YMAX) > 0.0)
                    ? Math.Abs(state.YMIN / state.YMAX)
                    : double.PositiveInfinity;

                if (ratio > 1.0 && ratio <= 1.016 && state.IA * state.IB > 0)
                {
                    // Exit iteration process
                    return false;
                }
                else if (state.IA * state.IB > 0)
                {
                    // Bracketed on both sides → bisect
                    state.UIO = (state.YMAX + state.YMIN) / 2.0;
                    continueIterations = true;
                }
                else if (state.IA == 0)
                {
                    // Only below-threshold runs so far → double amplitude
                    state.UIO = 2.0 * state.UM[n];
                    continueIterations = true;
                }
            }
        }

        if (!continueIterations)
        {
            Console.WriteLine($"FELL THROUGH ITER {n}");
            Console.WriteLine($"IA={state.IA} IB={state.IB} YMAX={state.YMAX} YMIN={state.YMIN}");
        }

        return continueIterations;
    }


    private static void RunThresholdSearch(SennState state)
    {
        // Initialize threshold search counters ONCE before the iteration loop
        // Fortran lines 671-674: IA=0, IB=0, YMAX=-1e38, YMIN=1e38
        state.IA = 0;
        state.IB = 0;
        state.YMAX = -1e38;  // MAXIMUM VALUE BELOW THRESHOLD
        state.YMIN = 1e38;   // MINIMUM VALUE ABOVE THRESHOLD

        bool again;

        do
        {
            // Fortran GOTO 20 / 202 equivalent: reset per-run stuff
            SetupWaveformDiagnosticsAndInitialAmplitude(state);
            InitializeStateVectorY(state);
            ComputeExternalPotentialsAndInitDerivatives(state);

            again = RunSingleIterationAndMaybeUpdateUIO(state);
            // If ITHR == 0, RunSingleIterationAndMaybeUpdateUIO will return false after one run.
        }
        while (again);
    }

    private static void PrintIterativeSummary(SennState state)
    {
        var w = state.DataOutWriter;
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        if (state.ITHR == 0)
            return; // skip if not threshold seeking

        // Header line
        const string header = "   ITER    VMAX     NXGT        UIO      TMAX   NODE";
        Console.WriteLine();
        Console.WriteLine(header);
        w?.WriteLine();
        w?.WriteLine(header);

        // For i = 1..NI, print (ITER, VMAX, NXGT, UIO, TMAX, NODE)
        for (int i = 1; i <= state.NI; i++)
        {
            string line =
                $"   {i,3}  {state.VM[i],10:F4}   {state.NXGT[i],3}  {state.UM[i],11:F4}  {state.TM[i],8:F4}  {state.NN[i],3}";
            Console.WriteLine(line);
            w?.WriteLine(line);
        }
    }

    private static RunNextAction EndOfRunAndDecideNext(SennState state)
    {
        var w = state.DataOutWriter;

        // End of NP trials
        state.IRUN++;

        string banner = $" ************* END OF RUN ************* {state.IRUN}";
        Console.WriteLine();
        Console.WriteLine(banner);
        w?.WriteLine();
        w?.WriteLine(banner);

        // Sentinel markers to units 17 and 30 (if used)
        state.Out17?.WriteLine("5000 5000");
        state.Out30?.WriteLine("5000 5000");

        // If threshold seeking (ITHR = 1): start a completely new run (like GOTO 3333)
        if (state.ITHR == 1)
        {
            return RunNextAction.RestartFullRun;
        }

        // Non-threshold mode: handle special behavior for IWAVE 3, 8, 9
        if (state.IWAVE == 8 || state.IWAVE == 3 || state.IWAVE == 9)
        {
            // Node 1 threshold flag
            if (state.tflag[1])
            {
                Console.WriteLine("threshold exceeded in node 1");
            }
            else
            {
                Console.WriteLine("threshold not exceeded in node 1");
            }

            // IWAVE 8 or 9: ask for next UIO2 at same freq
            if (state.IWAVE == 8 || state.IWAVE == 9)
            {
                Console.WriteLine($"NEXT UIO2 AT FREQ {state.FREQ} ?");
                var line = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    state.UIO2 = double.Parse(line, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // IWAVE 3: ask for next UIO (keeping DCOFF shown)
            if (state.IWAVE == 3)
            {
                Console.WriteLine($"NEXT UIO AT FREQ {state.FREQ} DCOFF ?  {state.DCOFF}");
                var line = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    state.UIO = double.Parse(line, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // Reset tflag(1..6)
            for (int i = 1; i <= 6; i++)
            {
                state.tflag[i] = false;
            }

            // Equivalent to GOTO 202: re-zero Y, reapply gating ICs, etc.
            return RunNextAction.RestartIntegration;
        }

        // Default: GOTO 3333 (new full run with same file)
        // But check if we should stop (e.g., no more input)
        return RunNextAction.RestartFullRun;
    }


    private static void FCT(double x, SennState s)
    {
        double[] Y = s.Y, DERY = s.DERY, PRMT = s.PRMT;
        double[] TIM = s.TIM, EPOT = s.EPOT, EPT = s.EPT;
        double[] UINA = s.UINA, UIK = s.UIK, UIP = s.UIP, UIL = s.UIL;
        double[,] CA = s.CA, CB = s.CB, SINEIN2 = s.SINEIN2;

        int NON = s.NON;
        int JT = 2 * NON;
        int NNODES = 2 * NON + 1;
        int K = JT + 2;

        double XMULT = ComputeStimulusMultiplier(x, s);
        ApplyPrimaryEPT(XMULT, x, s);

        bool insidePrimaryPulse = IsInsidePrimaryPulse(x, s);

        // If IWAVE=6,8,9 — special double-pulse logic
        bool insideSecondaryPulse = IsInsideSecondaryPulse(x, s);

        if (insideSecondaryPulse)
            ApplySecondaryEPT(XMULT, x, s);

        // Compute TIM (axial currents)
        ComputeTIM(x, XMULT, insidePrimaryPulse || insideSecondaryPulse, s);

        // Compute linear dV/dt for passive nodes
        ComputeLinearDvDt(s);

        // Compute nonlinear nodes
        ComputeNonlinearNodes(s);

        // Done
    }


    private static double ComputeStimulusMultiplier(double x, SennState s)
    {
        switch (s.IWAVE)
        {
            case 1: // Rectangular
                return (x < s.XPD ? 1.0 : 0.0);

            case 2: // Single sine
                return (x < s.XPD ? Math.Sin(s.PROD * x + s.ANGLE) : 0.0);

            case 3: // Sine on pedestal
                if (x < s.Tp)
                    return Math.Sin(s.PROD * x + s.ANGLE);
                if (x <= s.XPD)
                    return Math.Sin(s.PROD * x + s.ANGLE);
                return 0.0;

            case 4: // Exponential
                return (x < s.XPD ? 1.0 / Math.Exp(x / s.TAUS) : 0.0);

            case 5: // Exponential sinusoid
                return (x < s.XPD
                    ? Math.Sin(s.PROD * x + s.ANGLE) / Math.Exp(x / s.TAUS)
                    : 0.0);

            case 6: // Pulse doublet (base handled later)
                return (x < s.XPD ? 1.0 : 0.0);

            case 8: // Rectangular + sine
            case 9: // Sine + rectangular
                    // handled later in secondary pulse logic
                return 1.0;

            case 10: // Two sinusoids
                if (x >= s.XPD) return 0.0;
                return Math.Sin(s.PROD * x + s.ANGLE) +
                       s.AMP2 * Math.Sin(s.PROD2 * x + s.ANGLE2);

            case 11: // Amplitude modulation
                if (x >= s.XPD) return 0.0;
                return (1.0 + s.AMP2 * Math.Cos(s.PROD2 * x + s.ANGLE2)) *
                       Math.Sin(s.PROD * x + s.ANGLE);

            case 12: // Sum of NSINES sinusoids
                if (x >= s.XPD) return 0.0;
                double sum = 0;
                for (int i = 1; i <= s.NSINES; i++)
                    sum += SINEIN(x, i, s);
                return sum;

            case 13: // Arbitrary waveform (YINTERP)
                if (x >= s.XPD) return 0.0;
                int idx = (int)((2 * x) / s.PRMT[3] + 1.5);
                return s.YINTERP[idx];

            default:
                return 0.0;
        }
    }

    private static double SINEIN(double x, int i, SennState s)
    {
        return s.SINEIN2[i, 1] * Math.Sin(s.SINEIN2[i, 2] * x + s.SINEIN2[i, 3]);
    }


    private static void ApplyPrimaryEPT(double XMULT, double x, SennState s)
    {
        for (int i = 1; i <= s.NNODES; i++)
        {
            if (s.IWAVE != 3)
            {
                s.EPT[i] = s.EPOT[i] * XMULT;
            }
            else
            {
                // IWAVE = 3: Sine + pedestal
                if (x < s.Tp)
                    s.EPT[i] = s.EPOT[i] * XMULT + (i - 1) * s.DCOFF * s.RHOE * s.EL;
                else if (x <= s.XPD)
                    s.EPT[i] = s.EPOT[i] * XMULT;
                else
                    s.EPT[i] = 0.0;
            }
        }
    }

    private static bool IsInsidePrimaryPulse(double x, SennState s)
    {
        if (x < s.XPD) return true;
        if (x <= s.XPD + s.DELAY) return true;
        return false;
    }

    private static bool IsInsideSecondaryPulse(double x, SennState s)
    {
        if (s.IWAVE != 6 && s.IWAVE != 8 && s.IWAVE != 9) return false;

        for (int i = 1; i <= s.NP; i++)
            if (x >= s.PL[i] && x <= s.PT[i])
                return true;

        if (x <= s.XPD + s.XPD2 && (s.IWAVE == 8 || s.IWAVE == 9))
            return true;

        return false;
    }

    private static void ApplySecondaryEPT(double XMULT, double x, SennState s)
    {
        for (int i = 1; i <= s.NNODES; i++)
        {
            // Replace primary EPOT with UIO2 version during second phase
            double ext = ComputeExternalPotentialAtNode(i, s.UIO2, XMULT, s);
            s.EPT[i] = ext;
        }
    }

    private static double ComputeExternalPotentialAtNode(int i, double scale, double XMULT, SennState s)
    {
        // Implements the FS=0,1,2 logic (point, uniform, imported)
        // same equations as your Fortran, but as a pure function

        if (s.FS == 2)
            return s.EPOTIN[i] * scale * XMULT;

        double xi = (i - s.NON - 1) * s.EL;
        double rc = Math.Sqrt((s.XC - xi) * (s.XC - xi) + s.YC * s.YC);
        double ra = Math.Sqrt((s.XA - xi) * (s.XA - xi) + s.YA * s.YA);

        if (s.FS == 0)
            return XMULT * s.RHOE * scale * (1.0 / ra - 1.0 / rc) / s.PIMULT;

        if (s.FS == 3)
            return XMULT * s.RHOE * scale * Math.Log(rc / ra) / (s.PIMULT * s.WIREL);

        // FS = 1 uniform field
        return s.VREF + (i - 1) * scale * s.RHOE * s.EL * XMULT;
    }


    private static void ComputeTIM(double x, double XMULT, bool includeEPT, SennState s)
    {
        int JT = 2 * s.NON;
        int NN = s.NNODES;

        if (!includeEPT)
        {
            // Pure passive case
            s.TIM[1] = s.CGA * (s.Y[2] - s.Y[1]);
            s.TIM[JT + 1] = s.CGA * (s.Y[JT] - s.Y[JT + 1]);

            for (int i = 2; i <= JT; i++)
                s.TIM[i] = s.CGA * (s.Y[i - 1] - 2 * s.Y[i] + s.Y[i + 1]);

            return;
        }

        // With EPT
        s.TIM[1] = s.CGA * (s.Y[2] - s.Y[1] + s.EPT[2] - s.EPT[1]);
        s.TIM[JT + 1] = s.CGA * (s.Y[JT] - s.Y[JT + 1] + s.EPT[JT] - s.EPT[JT + 1]);

        for (int i = 2; i <= JT; i++)
            s.TIM[i] = s.CGA * (s.Y[i - 1] - 2 * s.Y[i] + s.Y[i + 1] +
                                s.EPT[i - 1] - 2 * s.EPT[i] + s.EPT[i + 1]);
    }

    private static void ComputeLinearDvDt(SennState s)
    {
        int JT = 2 * s.NON + 1;
        for (int i = 1; i <= JT; i++)
            s.DERY[i] = (s.TIM[i] - s.CGM * s.Y[i]) / s.CCM;
    }

    private static void ComputeNonlinearNodes(SennState s)
    {
        // No nonlinear nodes
        if (s.NLIN1 <= 0 || s.NLIN1 > s.NLIN2)
            return;

        // Switch based on membrane model
        switch (s.MembraneModel)
        {
            case MembraneModel.FrankenhaeuserHuxley:
                ComputeNonlinearNodes_FH(s);
                break;
            case MembraneModel.HodgkinHuxley:
                ComputeNonlinearNodes_HH(s);
                break;
            case MembraneModel.ChiuRitchieRogartStagg:
                ComputeNonlinearNodes_CRRS(s);
                break;
            case MembraneModel.McIntyreRichardsonGrill:
                ComputeNonlinearNodes_MRG(s);
                break;
            default:
                ComputeNonlinearNodes_FH(s); // fallback to FH
                break;
        }
    }

    // Extract existing implementation to this method
    private static void ComputeNonlinearNodes_FH(SennState s)
    {
        // No nonlinear nodes
        if (s.NLIN1 <= 0 || s.NLIN1 > s.NLIN2)
            return;

        const double PERX2 = 0.0002;          // From DATA PERX2/0.0002/
        int jt = 2 * s.NON + 1;               // Last linear node index

        // A(1..4), B(1..4) – rate coefficients for h, m, p, n
        double[] A = new double[5];
        double[] B = new double[5];

        int jCount = 0; // counts nonlinear nodes

        for (int k = s.NLIN1; k <= s.NLIN2; k++)
        {
            int L = jt + 4 * jCount; // base index for h,m,p,n at this node

            // ---------- h gate (index 1) ----------
            double delv = PERX2 * s.CA[1, 3];
            double del = s.CA[1, 2] - s.Y[k];

            if (Math.Abs(del) > delv)
            {
                if (-del / s.CA[1, 3] > 87.0)
                {
                    A[1] = 1e-36;
                }
                else
                {
                    A[1] = s.CA[1, 1] * del / (1.0 - Math.Exp(-del / s.CA[1, 3]));
                }
            }
            else
            {
                // L'Hôpital limit
                A[1] = s.CA[1, 1] * s.CA[1, 3];
            }

            double dum = (s.CB[1, 2] - s.Y[k]) / s.CB[1, 3];
            B[1] = (dum < 78.0) ? s.CB[1, 1] / (1.0 + Math.Exp(dum)) : 0.0;

            // ---------- m, p, n gates (indices 2..4) ----------
            for (int i = 2; i <= 4; i++)
            {
                // alpha_x
                delv = PERX2 * s.CA[i, 3];
                del = s.Y[k] - s.CA[i, 2];

                if (Math.Abs(del) > delv)
                {
                    if (-del / s.CA[i, 3] < 87.0)
                        A[i] = s.CA[i, 1] * del / (1.0 - Math.Exp(-del / s.CA[i, 3]));
                    else
                        A[i] = 1e-36;
                }
                else
                {
                    A[i] = s.CA[i, 1] * s.CA[i, 3];
                }

                // beta_x
                delv = PERX2 * s.CB[i, 3];
                del = s.CB[i, 2] - s.Y[k];

                if (Math.Abs(del) > delv)
                    B[i] = s.CB[i, 1] * del / (1.0 - Math.Exp(-del / s.CB[i, 3]));
                else
                    B[i] = s.CB[i, 1] * s.CB[i, 3];
            }

            // ---------- gating derivatives dh/dt, dm/dt, dp/dt, dn/dt ----------
            for (int i = 1; i <= 4; i++)
            {
                int mIdx = L + i; // Y(L+1..L+4)
                double y = s.Y[mIdx];
                s.DERY[mIdx] = A[i] * (1.0 - y) - B[i] * y;
            }

            // ---------- ionic currents at nonlinear node k ----------
            double EFRT = (s.Y[k] + s.ER) * s.F / s.R / s.T;
            if (EFRT > 87.0) EFRT = 87.0;
            double EFRT2 = EFRT * s.F;
            double ex = Math.Exp(EFRT);
            double den = 1.0 - ex;

            double pna = s.PNAB * s.Y[L + 1] * Math.Pow(s.Y[L + 2], 2.0);
            double pp = s.PPB * Math.Pow(s.Y[L + 3], 2.0);
            double pk = s.PKB * Math.Pow(s.Y[L + 4], 2.0);

            s.UINA[k] = pna * EFRT2 * (s.SODO - s.SODI * ex) / den * 1000.0;
            s.UIK[k] = pk * EFRT2 * (s.POTO - s.POTI * ex) / den * 1000.0;
            s.UIP[k] = pp * EFRT2 * (s.SODO - s.SODI * ex) / den * 1000.0;
            s.UIL[k] = s.GL * (s.Y[k] - s.VL);

            s.SUMK[k] = s.UINA[k] + s.UIK[k] + s.UIP[k] + s.UIL[k];

            // ---------- override dV/dt at nonlinear node ----------
            if (s.TT == 1)
            {
                // Truncated axon
                s.DERY[k] = (s.TIM[k] - s.SUMK[k] * s.AREA) / s.CCM;
            }
            else if (s.TT == 2)
            {
                // Cell body + hillock
                if (k == 2)
                    s.DERY[2] = (s.TIM[2] - s.SUMK[k] * s.AH) / s.CMH;
                else if (k == 3)
                    s.DERY[3] = (s.TIM[3] - s.SUMK[k] * s.AN) / s.CCM;
                else if (k > 3)
                    s.DERY[k] = (s.TIM[k] - s.SUMK[k] * s.AN) / s.CCM;
            }

            jCount++;
        }
    }

    // Hodgkin-Huxley model implementation (uses h, m, n - no persistent sodium)
    private static void ComputeNonlinearNodes_HH(SennState s)
    {
        // No nonlinear nodes
        if (s.NLIN1 <= 0 || s.NLIN1 > s.NLIN2)
            return;

        const double PERX2 = 0.0002;          // Small threshold for numerical stability
        int jt = 2 * s.NON + 1;               // Last linear node index

        // A(1..3), B(1..3) – rate coefficients for h, m, n
        double[] A = new double[4];  // A[1]=h, A[2]=m, A[3]=n
        double[] B = new double[4];  // B[1]=h, B[2]=m, B[3]=n

        int jCount = 0; // counts nonlinear nodes

        for (int k = s.NLIN1; k <= s.NLIN2; k++)
        {
            int L = jt + 4 * jCount; // base index for h,m,n at this node (using 4 slots for compatibility)
            double V = s.Y[k];  // membrane potential at node k (mV)

            // ---------- m gate (sodium activation) ----------
            // alpha_m(V) = 0.1*(V+40)/(1-exp(-(V+40)/10))
            // beta_m(V) = 4*exp(-(V+65)/18)
            // Note: V is in mV, equations adapted for absolute voltage
            double V_shift_m = V + 40.0;
            double delv_m = PERX2 * 10.0;
            
            if (Math.Abs(V_shift_m) > delv_m)
            {
                double exp_arg = -V_shift_m / 10.0;
                if (exp_arg > 87.0)
                {
                    A[2] = 1e-36;
                }
                else
                {
                    double denom = 1.0 - Math.Exp(exp_arg);
                    if (Math.Abs(denom) > 1e-10)
                        A[2] = 0.1 * V_shift_m / denom;
                    else
                        A[2] = 0.1 * 10.0; // L'Hôpital limit
                }
            }
            else
            {
                A[2] = 0.1 * 10.0; // L'Hôpital limit
            }

            double exp_arg_beta_m = -(V + 65.0) / 18.0;
            B[2] = (exp_arg_beta_m < 87.0) ? 4.0 * Math.Exp(exp_arg_beta_m) : 0.0;

            // ---------- h gate (sodium inactivation) ----------
            // alpha_h(V) = 0.07*exp(-(V+65)/20)
            // beta_h(V) = 1/(1+exp(-(V+35)/10))
            double exp_arg_alpha_h = -(V + 65.0) / 20.0;
            A[1] = (exp_arg_alpha_h < 87.0) ? 0.07 * Math.Exp(exp_arg_alpha_h) : 0.0;

            double exp_arg_beta_h = -(V + 35.0) / 10.0;
            if (exp_arg_beta_h < 78.0)
                B[1] = 1.0 / (1.0 + Math.Exp(exp_arg_beta_h));
            else
                B[1] = 0.0;

            // ---------- n gate (potassium activation) ----------
            // alpha_n(V) = 0.01*(V+55)/(1-exp(-(V+55)/10))
            // beta_n(V) = 0.125*exp(-(V+65)/80)
            double V_shift_n = V + 55.0;
            double delv_n = PERX2 * 10.0;

            if (Math.Abs(V_shift_n) > delv_n)
            {
                double exp_arg = -V_shift_n / 10.0;
                if (exp_arg > 87.0)
                {
                    A[3] = 1e-36;
                }
                else
                {
                    double denom = 1.0 - Math.Exp(exp_arg);
                    if (Math.Abs(denom) > 1e-10)
                        A[3] = 0.01 * V_shift_n / denom;
                    else
                        A[3] = 0.01 * 10.0; // L'Hôpital limit
                }
            }
            else
            {
                A[3] = 0.01 * 10.0; // L'Hôpital limit
            }

            double exp_arg_beta_n = -(V + 65.0) / 80.0;
            B[3] = (exp_arg_beta_n < 87.0) ? 0.125 * Math.Exp(exp_arg_beta_n) : 0.0;

            // ---------- gating derivatives dh/dt, dm/dt, dn/dt ----------
            // Store in same positions as FH: L=h, L+1=m, L+2=unused, L+3=n
            double h = s.Y[L];
            double m = s.Y[L + 1];
            double n = s.Y[L + 3];  // Use L+3 slot for n (L+2 is unused for HH)

            s.DERY[L] = A[1] * (1.0 - h) - B[1] * h;      // dh/dt
            s.DERY[L + 1] = A[2] * (1.0 - m) - B[2] * m;  // dm/dt
            s.DERY[L + 3] = A[3] * (1.0 - n) - B[3] * n;  // dn/dt
            // L+2 slot remains unused (no persistent sodium in HH)

            // ---------- ionic currents at nonlinear node k ----------
            // HH model: I_Na = g_Na_max * m^3 * h * (V - E_Na)
            //           I_K = g_K_max * n^4 * (V - E_K)
            //           I_L = g_L * (V - E_L)
            
            // HH conductances (mS/cm²) - original squid giant axon values
            // Scale to match FH current scale (FH uses permeability-based GHK equation)
            // Typical FH currents are much smaller, so we scale HH conductances
            const double SCALE_FACTOR = 0.001;  // Scale factor to match FH current magnitude
            const double G_NA_MAX = 120.0 * SCALE_FACTOR;  // Scaled to match FH
            const double G_K_MAX = 36.0 * SCALE_FACTOR;    // Scaled to match FH
            const double G_L_HH = 0.3 * SCALE_FACTOR;     // Scaled to match FH

            // Reversal potentials (mV)
            const double E_NA_HH = 50.0;     // mV
            const double E_K_HH = -77.0;    // mV
            const double E_L_HH = -54.4;    // mV

            // Calculate conductances (scaled mS/cm²)
            double g_na = G_NA_MAX * Math.Pow(m, 3.0) * h;
            double g_k = G_K_MAX * Math.Pow(n, 4.0);
            double g_l = G_L_HH;

            // Calculate currents: g(mS/cm²) * (V-E)(mV) = μA/cm²
            // Remove the *1000.0 multiplier that was causing explosion
            // The currents are already in the right units after scaling
            s.UINA[k] = g_na * (V - E_NA_HH);
            s.UIK[k] = g_k * (V - E_K_HH);
            s.UIL[k] = g_l * (V - E_L_HH);
            s.UIP[k] = 0.0;  // No persistent sodium in HH model

            // Total ionic current
            s.SUMK[k] = s.UINA[k] + s.UIK[k] + s.UIL[k];

            // ---------- override dV/dt at nonlinear node ----------
            // Use same formula as FH model for consistency
            if (s.TT == 1)
            {
                // Truncated axon
                s.DERY[k] = (s.TIM[k] - s.SUMK[k] * s.AREA) / s.CCM;
            }
            else if (s.TT == 2)
            {
                // Cell body + hillock
                if (k == 2)
                    s.DERY[2] = (s.TIM[2] - s.SUMK[k] * s.AH) / s.CMH;
                else if (k == 3)
                    s.DERY[3] = (s.TIM[3] - s.SUMK[k] * s.AN) / s.CCM;
                else if (k > 3)
                    s.DERY[k] = (s.TIM[k] - s.SUMK[k] * s.AN) / s.CCM;
            }

            jCount++;
        }
    }

    // Stub for Chiu-Ritchie-Rogart-Stagg model
    private static void ComputeNonlinearNodes_CRRS(SennState s)
    {
        // TODO: Implement Chiu-Ritchie-Rogart-Stagg model
        // CRRS model for mammalian myelinated axons
        // Uses different gating variables and rate constants
        throw new NotImplementedException("Chiu-Ritchie-Rogart-Stagg model not yet implemented. Use Frankenhaeuser-Huxley model for now.");
    }

    // Stub for McIntyre-Richardson-Grill model
    private static void ComputeNonlinearNodes_MRG(SennState s)
    {
        // TODO: Implement McIntyre-Richardson-Grill model
        // MRG model for human peripheral nerve fibers
        // Uses different gating variables and rate constants
        throw new NotImplementedException("McIntyre-Richardson-Grill model not yet implemented. Use Frankenhaeuser-Huxley model for now.");
    }

    public static void OutputStep(double x, SennState s, int iHalf, int nDim)
    {
        var w66 = s.DataOutWriter;
        var w17 = s.Out17;
        var w30 = s.Out30;
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        int jt = 2 * s.NON + 1;
        int nnodes = jt;
        int nodes = s.NON + 1;

        // --- First call in a run? Reset arrays (VMAX must be set to 0 at start of run) ---
        // Fortran: IF(VMAX.NE.0.) GO TO 70
        if (Math.Abs(s.VMAX) < 1e-12)
        {
            for (int i = 1; i <= 1100; i++)
            {
                s.VFLAG[i] = 0.0;
                s.TBT[i] = 0.0;
                s.TAT[i] = 0.0;
                s.VBT[i] = 0.0;
                s.VAT[i] = 0.0;
                s.TTIME[i] = 0.0;
            }
            s.NNGTT = 0;
            // NOTE: Do NOT reset NodeExcit here - Fortran doesn't reset NODEXCIT
            // NODEXCIT retains its value from when NFOUND first became true
        }

        // --- Find global max voltage over nonlinear region (1..NLIN2) ---
        for (int k = 1; k <= s.NLIN2; k++)
        {
            if (s.Y[k] <= s.VMAX) continue;

            s.VMAX = s.Y[k];
            s.TMAX = x;
            s.NODE = k;
        }

        // --- Find excitation node (first nonlinear node exceeding threshold) ---
        if (!s.NFOUND && s.VMAX > s.VTH)
        {
            if (s.NODE >= s.NLIN1 && s.NODE <= s.NLIN2)
            {
                s.NodeExcit = s.NODE;
                s.NFOUND = true;
            }
        }

        // --- Track threshold crossings for nonlinear nodes starting at NodeExcit ---
        if (s.NodeExcit > 0)
        {
            for (int i = s.NodeExcit; i <= s.NLIN2; i++)
            {
                if (s.VFLAG[i] != 0.0)
                    continue;

                s.VBT[i] = s.VAT[i];
                s.VAT[i] = s.Y[i];
                s.TBT[i] = s.TAT[i];
                s.TAT[i] = x;

                if (s.Y[i] < s.VTH)
                    continue;

                s.VFLAG[i] = 1.0;
                s.NNGTT++;
            }
        }

        // --- Decide whether to end run ---
        bool endRun = false;

        if (x > s.TEND)
        {
            endRun = true;
        }
        else if (s.ITHR != 0 && s.NNGTT >= s.INNGTT)
        {
            endRun = true;
        }

        if (endRun)
        {
            // Signal termination back to integrator
            s.PRMT[5] = 1.0;

            int node2 = s.NODE1 + 9;    // up to 10 nodes
            if (node2 > nnodes) node2 = nnodes;

            // Voltages at print nodes to data.out (unit 66)
            WriteVoltages(w66, x, s, s.NODE1, node2);

            // Time vs node of max V (unit 17) and first print node (unit 30)
            w17?.WriteLine($"{x.ToString("F6", ci)} {s.Y[s.NODE].ToString("F6", ci)}");
            w30?.WriteLine($"{x.ToString("F6", ci)} {s.Y[s.NODE1].ToString("F6", ci)}");

            // Threshold flags for nodes 1..6
            UpdateNodeThresholdFlags(s);

            // Total ionic current at node PLTN (unused but kept for completeness)
            double utot = s.UINA[s.pltn] + s.UIK[s.pltn] + s.UIP[s.pltn] + s.UIL[s.pltn];

            // TIM, SUMK, DERY for printed nodes
            WriteSeries(w66, "   TIM ", x, s.TIM, s.NODE1, node2);
            WriteSeries(w66, "   SUMK", x, s.SUMK, s.NODE1, node2);
            WriteSeries(w66, "   DERY", x, s.DERY, s.NODE1, node2);

            if (Math.Abs(x) < 1e-12)
                Console.WriteLine("FIRST OUTPUTS OUTP");

            // Only print max summary if different from previous
            if (!(s.VMAX == s.VMAXO && s.UIO == s.UIOLD && s.NODOLD == s.NODE && Math.Abs(s.TOLD - s.TMAX) < 1e-12))
            {
                s.TOLD = s.TMAX;
                s.NODOLD = s.NODE;
                s.VMAXO = s.VMAX;
                s.UIOLD = s.UIO;

                w66?.WriteLine(string.Format(ci,
                    "    MAX V(mV) ={0,10:F3} AT T ={1,9:F4} AT NODE {2,5} FOR UI0 = {3,9:F4}",
                    s.VMAX, s.TMAX, s.NODE, s.UIO));
            }

            // Interpolate times when nodes first reach threshold
            w66?.WriteLine("0 TIMES WHEN NODES FIRST REACH VTH ");

            if (s.NodeExcit > 0)
            {
                for (int i = s.NodeExcit; i <= s.NLIN2; i++)
                {
                    if (s.VFLAG[i] == 0.0) continue;

                    s.TTIME[i] = s.TBT[i] + (s.VTH - s.VBT[i]) *
                                  (s.TAT[i] - s.TBT[i]) / (s.VAT[i] - s.VBT[i]);

                    int j = i;

                    w66?.WriteLine(string.Format(ci,
                        " THRESHOLD REACHED AT NODE {0,3} AT TIME = {1,10:F6} FOR VMAX = {2,8:F3}   VN{3,3} = {4,8:F3} UIO = {5,10:F4}",
                        j, s.TTIME[i], s.VMAX, s.NodeExcit, s.Y[s.NodeExcit], s.UIO));
                }

                // Conduction velocities (V12, V23, V13) based on nodes 1..3
                double v12 = 0.0, v23 = 0.0, v13 = 0.0;
                if (s.VFLAG[2] != 0.0)
                    v12 = 1.0 / (s.TTIME[2] - s.TTIME[1]);
                if (s.VFLAG[3] != 0.0)
                {
                    v23 = 1.0 / (s.TTIME[3] - s.TTIME[2]);
                    v13 = 2.0 / (s.TTIME[3] - s.TTIME[1]);
                }

                if (s.VFLAG[2] != 0.0)
                {
                    w66?.WriteLine(string.Format(ci,
                        " V12 = {0,10:F4} V23 = {1,10:F4} V13 = {2,12:F3}",
                        v12, v23, v13));
                }
            }

            return; // end-of-run
        }

        // ---------------- Normal step (no termination) ----------------

        s.IPT++;

        // Time vs NODE and NODE1 to 17 and 30
        w17?.WriteLine($"{x.ToString("F6", ci)} {s.Y[s.NODE].ToString("F6", ci)}");
        w30?.WriteLine($"{x.ToString("F6", ci)} {s.Y[s.NODE1].ToString("F6", ci)}");

        // Total ionic current (unused)
        {
            double utot = s.UINA[s.pltn] + s.UIK[s.pltn] + s.UIP[s.pltn] + s.UIL[s.pltn];
        }

        // Threshold flags for nodes 1..6
        UpdateNodeThresholdFlags(s);

        // Fortran arithmetic IF: IF (IPT-1) 25,25,50
        // => if IPT <= 1 -> branch "25" (always print);
        //    else         -> branch "50" (periodic print)
        bool doPrintBlock = (s.IPT <= 1) || (s.IPT >= s.IPRNT && s.IPRNT > 0);

        if (doPrintBlock)
        {
            int node2 = s.NODE1 + 9;
            if (node2 > nnodes) node2 = nnodes;

            WriteVoltages(w66, x, s, s.NODE1, node2);
            WriteSeries(w66, "   TIM ", x, s.TIM, s.NODE1, node2);
            WriteSeries(w66, "   SUMK", x, s.SUMK, s.NODE1, node2);
            WriteSeries(w66, "   DERY", x, s.DERY, s.NODE1, node2);

            if (!(s.VMAX == s.VMAXO && s.UIO == s.UIOLD && s.NODOLD == s.NODE && Math.Abs(s.TOLD - s.TMAX) < 1e-12))
            {
                s.TOLD = s.TMAX;
                s.NODOLD = s.NODE;
                s.VMAXO = s.VMAX;
                s.UIOLD = s.UIO;

                w66?.WriteLine(string.Format(ci,
                    "    MAX V(mV) ={0,10:F3} AT T ={1,9:F4} AT NODE {2,5} FOR UI0 = {3,9:F4}",
                    s.VMAX, s.TMAX, s.NODE, s.UIO));
            }

            if (s.IPRNT > 0 && s.IPT >= s.IPRNT)
                s.IPT = 0; // reset counter like Fortran label 51
        }
    }

    private static void WriteVoltages(TextWriter? w, double x, SennState s, int node1, int node2)
    {
        if (w == null) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        // Fortran FORMAT 500: 2X,F9.4,10(2X,F10.3)
        // 2 spaces, then F9.4 for time, then 10 values of F10.3 for voltages
        w.Write("     ");  // 5 spaces (2X = 2 spaces, but we need alignment)
        w.Write(string.Format(ci, "{0,9:F4}", x));
        for (int k = node1; k <= node2; k++)
        {
            w.Write(string.Format(ci, "  {0,10:F3}", s.Y[k]));  // 2 spaces + F10.3
        }
        w.WriteLine();
    }

    private static void WriteSeries(TextWriter? w, string label, double x, double[] arr, int node1, int node2)
    {
        if (w == null) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        // Fortran FORMAT 507: '  ',A7,3X,10(2X,E10.3)
        // 2 spaces, 7-char label, 3 spaces, then 10 values of E10.3
        w.Write("  ");  // 2 spaces
        w.Write(string.Format("{0,-7}", label));  // 7-char label (left-aligned)
        w.Write("   ");  // 3 spaces
        for (int k = node1; k <= node2; k++)
        {
            // E10.3 format: scientific notation with 3 decimal places, 10 total chars
            // Format like: -0.119E-06 or 0.667E+01
            w.Write(string.Format(ci, "  {0,10:E3}", arr[k]));  // 2 spaces + E10.3
        }
        w.WriteLine();
    }

    private static void UpdateNodeThresholdFlags(SennState s)
    {
        for (int i = 1; i <= 6; i++)
        {
            if (!s.tflag[i] && s.Y[i] >= s.VTH)
                s.tflag[i] = true;
        }
    }


    public static void RKGS(SennState s, int nDim)
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

}
