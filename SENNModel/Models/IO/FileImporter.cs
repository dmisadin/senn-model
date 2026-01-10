using System;
using System.Globalization;
using System.IO;

namespace SENNModel.Models.IO;

public class FileImporter
{
    public InputParams? TryReadInputParamsFromFile(TextReader inParamReader)
    {
        InputParams inputParams = new InputParams();
        try
        {
            string? line = inParamReader.ReadLine();

            if (line is null) return null;

            string? currentSection = null;  // "FIBER", "STIMULUS", "CONTROL"

            // Use invariant culture for parsing doubles with '.' decimal
            var ci = CultureInfo.InvariantCulture;

            // Fortran: READ(7,6666,END=5000)MES2
            // Read the descriptor line (20A4 format = 80 characters, stored in MES2 array)

            // Store descriptor (first line is the run descriptor)
            inputParams.DESCRIPTOR = line.Trim();

            while ((line = inParamReader.ReadLine()) != null)
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
                                ParseFiberField(inputParams, name, value, ci);
                                break;
                            case "STIMULUS":
                                ParseStimulusField(inputParams, name, value, ci);
                                break;
                            case "CONTROL":
                                ParseControlField(inputParams, name, value, ci);
                                break;
                        }
                    }
                }
            }

            return inputParams;

        } 
        catch (Exception e)
        {
            throw new Exception(e.Message);
        }
    }

    private void ParseFiberField(InputParams inparams, string name, string value, IFormatProvider ci)
    {
        switch (name)
        {
            case "NNODES":
                inparams.NNODES = (short)int.Parse(value, ci);
                break;
            case "NLIN1":
                inparams.NLIN1 = (short)int.Parse(value, ci);
                break;
            case "NLIN2":
                inparams.NLIN2 = (short)int.Parse(value, ci);
                break;
            case "NODE1":
                inparams.NODE1 = (short)int.Parse(value, ci);
                break;
            case "DIAM":
                inparams.DIAM = double.Parse(value, ci);
                break;
            case "GAP":
                inparams.GAP = double.Parse(value, ci);
                break;
            case "CM":
                inparams.CM = double.Parse(value, ci);
                break;
            case "GM":
                inparams.GM = double.Parse(value, ci);
                break;
            case "RHOI":
                inparams.RHOI = double.Parse(value, ci);
                break;
            case "RHOE":
                inparams.RHOE = double.Parse(value, ci);
                break;
        }
    }

    private void ParseStimulusField(InputParams inparams, string name, string value, IFormatProvider ci)
    {
        switch (name)
        {
            case "XC":
                inparams.XC = double.Parse(value, ci);
                break;
            case "YC":
                inparams.YC = double.Parse(value, ci);
                break;
            case "XA":
                inparams.XA = double.Parse(value, ci);
                break;
            case "YA":
                inparams.YA = double.Parse(value, ci);
                break;
            case "WIREL":
                inparams.WIREL = double.Parse(value, ci);
                break;
            case "IWAVE":
                inparams.IWAVE = (short)int.Parse(value, ci);
                break;
            case "UIO":
                inparams.UIO = double.Parse(value, ci);
                break;
            case "XPD":
                inparams.XPD = double.Parse(value, ci);
                break;
            case "UIO2":
                inparams.UIO2 = double.Parse(value, ci);
                break;
            case "XPD2":
                inparams.XPD2 = double.Parse(value, ci);
                break;
            case "DELAY":
                inparams.DELAY = double.Parse(value, ci);
                break;
            case "FREQ":
                inparams.FREQ = double.Parse(value, ci);
                break;
            case "PHASE":
                inparams.PHASE = double.Parse(value, ci);
                break;
            case "FREQ2":
                inparams.FREQ2 = double.Parse(value, ci);
                break;
            case "PHASE2":
                inparams.PHASE2 = double.Parse(value, ci);
                break;
            case "AMP2":
                inparams.AMP2 = double.Parse(value, ci);
                break;
            case "NSINES":
                inparams.NSINES = (short)int.Parse(value, ci);
                break;
            case "DCOFF":
                inparams.DCOFF = double.Parse(value, ci);
                break;
            case "TAUS":
                inparams.TAUS = double.Parse(value, ci);
                break;
            case "VREF":
                inparams.VREF = double.Parse(value, ci);
                break;
            case "NP":
                inparams.NP = (short)int.Parse(value, ci);
                break;
            case "FS":
                inparams.FS = (short)int.Parse(value, ci);
                break;
            case "S":
                inparams.S = (short)int.Parse(value, ci);
                break;
            case "NTRP":
                inparams.NTRP = int.Parse(value, ci);
                break;
        }
    }

    private void ParseControlField(InputParams inparams, string name, string value, IFormatProvider ci)
    {
        switch (name)
        {
            case "ITHR":
                inparams.ITHR = (short)int.Parse(value, ci);
                break;
            case "VTH":
                inparams.VTH = double.Parse(value, ci);
                break;
            case "NTHNODE":
                inparams.NTHNODE = (short)int.Parse(value, ci);
                break;
            case "DELT":
                inparams.DELT = double.Parse(value, ci);
                break;
            case "DELT2M":
                inparams.DELT2M = double.Parse(value, ci);
                break;
            case "FINAL":
                inparams.FINAL = double.Parse(value, ci);
                break;
            case "IPRNT":
                inparams.IPRNT = (short)int.Parse(value, ci);
                break;
                // TT, DELT2, pltn keep their defaults (set elsewhere)
        }
    }
}
