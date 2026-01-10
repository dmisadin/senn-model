using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SENNModel.Models.IO;

public class FileExporter
{
    /// <summary>
    /// Generate Excel file with plots from plot_17.txt and plot_30.txt
    /// Each iteration is placed in separate column pairs, and all iterations are plotted on a single chart
    /// </summary>
    public void GenerateExcelPlots(string outputFile = "plots.xlsx", string plot17File = "plot_17.txt", string plot30File = "plot_30.txt")
    {
        if (!File.Exists(plot17File) && !File.Exists(plot30File))
        {
            Console.WriteLine($"Warning: Neither {plot17File} nor {plot30File} found. Skipping Excel generation.");
            return;
        }

        var ci = CultureInfo.InvariantCulture;
        var iterations17 = new List<List<(double X, double Y)>>();
        var iterations30 = new List<List<(double X, double Y)>>();

        // Parse plot_17.txt
        if (File.Exists(plot17File))
        {
            iterations17 = ParsePlotFile(plot17File, ci);
        }

        // Parse plot_30.txt
        if (File.Exists(plot30File))
        {
            iterations30 = ParsePlotFile(plot30File, ci);
        }

        // Create Excel workbook
        using (var workbook = new XLWorkbook())
        {
            // Create worksheet for plot_17 data
            if (iterations17.Count > 0)
            {
                CreatePlotWorksheet(workbook, iterations17, "Plot_17_NodeMaxV", "Time (ms)", "Voltage at Max V Node (mV)");
            }

            // Create worksheet for plot_30 data
            if (iterations30.Count > 0)
            {
                CreatePlotWorksheet(workbook, iterations30, "Plot_30_Node1", "Time (ms)", "Voltage at Node1 (mV)");
            }

            // Save workbook
            workbook.SaveAs(outputFile);
            Console.WriteLine($"Excel file created: {outputFile}");
        }
    }

    /// <summary>
    /// Parse a plot file, splitting iterations by the sentinel "0 0"
    /// </summary>
    private List<List<(double X, double Y)>> ParsePlotFile(string filename, CultureInfo ci)
    {
        var iterations = new List<List<(double X, double Y)>>();
        var currentIteration = new List<(double X, double Y)>();

        using (var reader = new StreamReader(filename))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                // Parse X Y values (space-separated)
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    if (double.TryParse(parts[0], NumberStyles.Float, ci, out double x) &&
                        double.TryParse(parts[1], NumberStyles.Float, ci, out double y))
                    {
                        if (x == 5000 && y == 5000)
                            continue;
                        // Check for sentinel value "0 0" that marks start of new iteration
                        if (Math.Abs(x) < 1e-10 && Math.Abs(y) < 1e-10)
                        {
                            // End of current iteration - save it if it has data
                            if (currentIteration.Count > 0)
                            {
                                iterations.Add(new List<(double X, double Y)>(currentIteration));
                                currentIteration.Clear();
                            }
                            // Don't add the sentinel itself to the data
                            continue;
                        }

                        currentIteration.Add((x, y));
                    }
                }
            }

            // Add last iteration if it has data (no sentinel after last iteration)
            if (currentIteration.Count > 0)
            {
                iterations.Add(currentIteration);
            }
        }

        return iterations;
    }

    /// <summary>
    /// Create a worksheet with plot data
    /// </summary>
    private void CreatePlotWorksheet(XLWorkbook workbook, List<List<(double X, double Y)>> iterations, string sheetName, string xAxisTitle, string yAxisTitle)
    {
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Write headers
        int col = 1;
        for (int iterationIndex = 0; iterationIndex < iterations.Count; iterationIndex++)
        {
            // X column header
            worksheet.Cell(1, col).Value = $"Iteration {iterationIndex + 1} - {xAxisTitle}";
            worksheet.Cell(1, col).Style.Font.Bold = true;

            // Y column header
            worksheet.Cell(1, col + 1).Value = $"Iteration {iterationIndex + 1} - {yAxisTitle}";
            worksheet.Cell(1, col + 1).Style.Font.Bold = true;

            col += 2;
        }

        // Find maximum number of data points across all iterations
        int maxRows = iterations.Max(i => i.Count);

        // Write data for each iteration
        for (int row = 2; row <= maxRows + 1; row++)
        {
            col = 1;
            for (int iterationIndex = 0; iterationIndex < iterations.Count; iterationIndex++)
            {
                var iteration = iterations[iterationIndex];
                int dataIndex = row - 2; // Convert to 0-based index

                if (dataIndex < iteration.Count)
                {
                    worksheet.Cell(row, col).Value = iteration[dataIndex].X;
                    worksheet.Cell(row, col + 1).Value = iteration[dataIndex].Y;
                }
                // Leave empty if this iteration has fewer data points

                col += 2;
            }
        }

        // NOTE: ClosedXML doesn't support adding charts directly to worksheets
        // The data is organized in column pairs (X, Y) for each iteration, making it easy
        // to create charts manually in Excel or use EPPlus library for programmatic chart creation
        if (iterations.Count > 0)
        {
            // Add a note about chart creation
            int noteRow = maxRows + 3;
            worksheet.Cell(noteRow, 1).Value = "Note: To create charts, select the data columns and insert an XY Scatter chart in Excel.";
            worksheet.Cell(noteRow, 1).Style.Font.Italic = true;
            worksheet.Cell(noteRow, 1).Style.Font.FontColor = XLColor.Gray;

            // Optional: Format the data ranges to make them easier to select
            // You can also add named ranges for easier chart creation
            for (int iterationIndex = 0; iterationIndex < iterations.Count; iterationIndex++)
            {
                int xCol = iterationIndex * 2 + 1;
                int yCol = iterationIndex * 2 + 2;
                int lastRow = iterations[iterationIndex].Count + 1;

                // Create named ranges for each iteration's X and Y data
                // Include sheet name in range name to make them unique across worksheets
                string xRangeName = $"{sheetName}_Iteration{iterationIndex + 1}_X";
                string yRangeName = $"{sheetName}_Iteration{iterationIndex + 1}_Y";

                string xRangeAddress = $"{sheetName}!${GetColumnLetter(xCol)}${2}:${GetColumnLetter(xCol)}${lastRow}";
                string yRangeAddress = $"{sheetName}!${GetColumnLetter(yCol)}${2}:${GetColumnLetter(yCol)}${lastRow}";

                // Check if named range already exists before adding
                if (!worksheet.Workbook.NamedRanges.Any(nr => nr.Name == xRangeName))
                {
                    worksheet.Workbook.NamedRanges.Add(xRangeName, xRangeAddress);
                }
                if (!worksheet.Workbook.NamedRanges.Any(nr => nr.Name == yRangeName))
                {
                    worksheet.Workbook.NamedRanges.Add(yRangeName, yRangeAddress);
                }
            }
        }
    }

    /// <summary>
    /// Convert column number (1-based) to Excel column letter (A, B, C, ..., Z, AA, AB, ...)
    /// </summary>
    private string GetColumnLetter(int columnNumber)
    {
        string columnLetter = "";
        while (columnNumber > 0)
        {
            columnNumber--;
            columnLetter = (char)('A' + (columnNumber % 26)) + columnLetter;
            columnNumber /= 26;
        }
        return columnLetter;
    }
}
