using ClosedXML.Excel;

namespace GolfClubSystem.Services;

public class ExcelReports
{
    public class WorkerTimesheet
    {
        public string FullName { get; set; }
        public string Position { get; set; }
        public List<(string Arrival, string Departure, double Hours)> Days { get; set; }
    }

    public class WorkerMonthly
    {
        public string FullName { get; set; }
        public string Position { get; set; }
        public List<string> Days { get; set; } // 23 days
        public int WorkedDays { get; set; }
        public double WorkedHours { get; set; }
    }

    public void GenerateTimesheetReport(string templatePath, string outputPath, List<WorkerTimesheet> workers, DateTime startDate, DateTime endDate)
    {
        using (var workbook = new XLWorkbook(templatePath))
        {
            var ws = workbook.Worksheet("Отчет");
            int daysCount = (endDate - startDate).Days;

            // Populate dynamic date headers
            ws.Cell(2, 2).Value = "Дата";
            for (int i = 0; i < daysCount; i++)
            {
                int colOffset = 3 + i * 3;
                
                var dateRange = ws.Range(2, colOffset, 2, colOffset + 2);
                dateRange.Merge();
                dateRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; // Center the date
                dateRange.Value = startDate.AddDays(i).ToString("dd.MM.yy");
                
                ws.Cell(3, colOffset).Value = "Приход";
                ws.Cell(3, colOffset + 1).Value = "Уход";
                ws.Cell(3, colOffset + 2).Value = "Кол-во часов";
            }

            // Populate worker data
            int row = 4;
            foreach (var worker in workers)
            {
                ws.Cell(row, 1).Value = worker.FullName;
                ws.Cell(row, 2).Value = worker.Position;
                for (int day = 0; day < daysCount; day++)
                {
                    int colOffset = 3 + day * 3; // Recalculate per day
                    ws.Cell(row, colOffset).Value = worker.Days[day].Arrival;
                    ws.Cell(row, colOffset + 1).Value = worker.Days[day].Departure;
                    ws.Cell(row, colOffset + 2).Value = worker.Days[day].Hours;
                }
                row++;
            }

            // Center all cells
            ws.Cells().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cells().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Adjust column widths
            ws.Columns().AdjustToContents();

            workbook.SaveAs(outputPath);
        }
    }

    public void GenerateMonthlyReport(string templatePath, string outputPath, List<WorkerMonthly> workers)
    {
        using var workbook = new XLWorkbook(templatePath);
        var ws = workbook.Worksheet("Отчет за месяц"); // Adjust if needed

        // Populate worker data
        var row = 3;
        var id = 1;
        
        foreach (var worker in workers)
        {
            ws.Cell(row, 1).Value = id++;
            ws.Cell(row, 2).Value = worker.FullName;
            ws.Cell(row, 3).Value = worker.Position;
            for (int day = 0; day < worker.Days.Count && day < 23; day++)
            {
                ws.Cell(row, 4 + day).Value = worker.Days[day];
            }

            ws.Cell(row, 27).Value = worker.WorkedDays; // AA
            ws.Cell(row, 28).Value = worker.WorkedHours; // AB
            row++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
    }
}