using Microsoft.Extensions.DependencyInjection;
using SENNModel.Models.Enums;
using SENNModel.Models.IO;
using SENNModel.Models.Simulations;
using System;
using System.IO;

namespace SENNModel.Models;

public class SennRunner
{
    private readonly IServiceProvider serviceProvider;
    private readonly FileImporter fileImporter;
    private readonly FileExporter fileExporter;
    private ISimulation? simulationMethod;

    public SennRunner(IServiceProvider serviceProvider,
                    FileImporter fileImporter,
                    FileExporter fileExporter)
    {
        this.serviceProvider = serviceProvider;
        this.fileImporter = fileImporter;
        this.fileExporter = fileExporter;
    }

    /// <summary>
    /// Run simulation with parameters from InputParams (GUI input)
    /// </summary>
    public void Run(InputParams inputParams)
    {
        var state = InitializeSimulationState();

        this.simulationMethod = serviceProvider.GetRequiredKeyedService<ISimulation>(state.MembraneModel);
        var resolvedOutputDir = new DirectoryInfo(AppContext.BaseDirectory);

        ApplyInputParamsToState(state, inputParams);
        InitializeOutputFiles(state, resolvedOutputDir);
        // Main loop: corresponds to label 3333 (start of run)
        // Fortran: GOTO 3333 can restart from the beginning
        while (true)
        {
            try
            {
                // Parameters already applied, skip file reading
                state.Descriptor = inputParams.DESCRIPTOR ?? "SINUSOID";

                if (this.simulationMethod.ExecuteSimulationStep(state) == RunNextAction.Stop)
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

    public void Run(MembraneModel membraneModel)
    {
        Run(membraneModel, null);
    }

    /// <summary>
    /// Run simulation with parameters from file (original behavior)
    /// </summary>
    public void Run(MembraneModel membraneModel, DirectoryInfo? outputDir)
    {
        var state = InitializeSimulationState();
        var resolvedOutputDir = outputDir ?? new DirectoryInfo(AppContext.BaseDirectory);

        // OPEN(UNIT=7,FILE='inparam.txt',STATUS='OLD',ACCESS='SEQUENTIAL')
        // => open existing file for reading
        state.InParamReader = new StreamReader("inparam.txt");
        state.MembraneModel = membraneModel;

        this.simulationMethod = serviceProvider.GetRequiredKeyedService<ISimulation>(state.MembraneModel);

        InitializeOutputFiles(state, resolvedOutputDir);

        // Main loop: corresponds to label 3333 (start of run)
        // Fortran: GOTO 3333 can restart from the beginning
        while (true)
        {
            try
            {
                // Label 1: Read input parameters (with EOF handling)
                // Fortran: READ(7,6666,END=5000)MES2
                InputParams? inputParams = fileImporter.TryReadInputParamsFromFile(state.InParamReader);

                if (inputParams == null)
                {
                    Console.WriteLine("HIT EOF ON INPUT"); // EOF reached (label 5000)
                    break;
                }

                ApplyInputParamsToState(state, inputParams);

                RunNextAction? nextAction = this.simulationMethod.ExecuteSimulationStep(state);
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
                else if (nextAction == RunNextAction.RestartFullRun && inputParams == null)
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

    private SennState InitializeSimulationState()
    {
        var state = new SennState();

        state.IRUN = 0; // IRUN = 0 ! run counter

        return state;
    }

    private void InitializeOutputFiles(SennState state, DirectoryInfo outputDir)
    {
        if (!outputDir.Exists)
            outputDir.Create();

        string membraneModel = state.MembraneModel.GetDescription();
        string startedAt = state.StartedAt.ToString("yyyy-MM-dd-HH-mm-ss.ss");

        string dataOutFileName = $"data_{startedAt}_{membraneModel}.out";
        string out17FileName = $"plot_{startedAt}_17_{membraneModel}.txt";
        string out30FileName = $"plot_{startedAt}_30_{membraneModel}.txt";

        string dataOutPath = Path.Combine(outputDir.FullName, dataOutFileName);
        string out17Path = Path.Combine(outputDir.FullName, out17FileName);
        string out30Path = Path.Combine(outputDir.FullName, out30FileName);

        state.DataOutWriter = new StreamWriter(dataOutPath);
        state.Out17 = new StreamWriter(out17Path);
        state.Out30 = new StreamWriter(out30Path);

        state.DataOutFileName = dataOutPath;
        state.Out17FileName = out17Path;
        state.Out30FileName = out30Path;
    }

    /// <summary>
    /// Clean up simulation state and close files
    /// </summary>
    private void CleanupSimulationState(SennState state)
    {
        state.DataOutWriter?.Dispose();
        state.InParamReader?.Dispose();
        state.Out17?.Dispose();
        state.Out30?.Dispose();

        // Generate Excel plots after closing the text files
        try
        {
            var startedAt = state.StartedAt;
            string membraneModel = state.MembraneModel.GetDescription();
            string outputFileName = $"plot_{startedAt.ToString("yyyy-MM-dd-HH-mm.ss")}_{membraneModel}.xlsx";

            fileExporter.GenerateExcelPlots(outputFileName, state.Out17FileName, state.Out30FileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to generate Excel plots: {ex.Message}");
        }
    }

    private void ApplyInputParamsToState(SennState state, InputParams input)
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
}
