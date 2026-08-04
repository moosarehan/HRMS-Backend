using System.Text;
using HRMS_BACKEND.Dto.Attendance;
using HRMS_BACKEND.IServices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace HRMS_BACKEND.Services;

public class ExportService : IExportService
{
    public byte[] ToTxt(List<AttendanceExportDto> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Employee\tEmail\tBranch\tDepartment\tRole\tShift\tDate\tStart Time\tEnd Time\tClock In\tClock Out\tMinutes Late\tStatus");
        
        foreach (var r in records)
        {
            sb.AppendLine($"{r.EmployeeName}\t{r.Email}\t{r.Branch}\t{r.Department}\t{r.Role}\t{r.Shift}\t" +
                $"{r.Date:yyyy-MM-dd}\t" +
                $"{r.StartTime:HH:mm}\t" +
                $"{r.EndTime:HH:mm}\t" +
                $"{(r.ClockIn.HasValue ? r.ClockIn.Value.ToString("HH:mm") : "-")}\t" +
                $"{(r.ClockOut.HasValue ? r.ClockOut.Value.ToString("HH:mm") : "Not clocked out")}\t" +
                $"{r.MinutesLate}\t{r.Status}");
        }
        
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ToPdf(List<AttendanceExportDto> records)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        using var stream = new MemoryStream();
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("HRMS Attendance Report")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                        column.Item().Text($"Generated on {DateTime.Now:dd MMM yyyy HH:mm}")
                            .FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2f);  // Employee Name
                        c.RelativeColumn(1.2f); // Branch
                        c.RelativeColumn(1.2f); // Department
                        c.RelativeColumn(0.8f); // Role
                        c.RelativeColumn(1.2f); // Shift
                        c.RelativeColumn(0.8f); // Date
                        c.RelativeColumn(0.8f); // Start Time
                        c.RelativeColumn(0.8f); // End Time
                        c.RelativeColumn(0.8f); // Clock In
                        c.RelativeColumn(0.8f); // Clock Out
                        c.RelativeColumn(1f);   // Minutes Late
                        c.RelativeColumn(0.8f); // Status
                    });

                    // Header
                    table.Header(h =>
                    {
                        var headers = new[] { "Employee", "Branch", "Department", "Role", "Shift", "Date", "Start", "End", "Clock In", "Clock Out", "Minutes Late", "Status" };
                        foreach (var title in headers)
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(title).Bold().FontSize(8);
                        }
                    });

                    // Data rows
                    foreach (var r in records)
                    {
                        table.Cell().Padding(3).Text(r.EmployeeName).FontSize(7);
                        table.Cell().Padding(3).Text(r.Branch).FontSize(7);
                        table.Cell().Padding(3).Text(r.Department).FontSize(7);
                        table.Cell().Padding(3).Text(r.Role).FontSize(7);
                        table.Cell().Padding(3).Text(r.Shift).FontSize(7);
                        table.Cell().Padding(3).Text(r.Date.ToString("dd/MM/yy")).FontSize(7);
                        table.Cell().Padding(3).Text(r.StartTime.ToString("HH:mm")).FontSize(7);
                        table.Cell().Padding(3).Text(r.EndTime.ToString("HH:mm")).FontSize(7);
                        table.Cell().Padding(3).Text(r.ClockIn?.ToString("HH:mm") ?? "-").FontSize(7);
                        table.Cell().Padding(3).Text(r.ClockOut?.ToString("HH:mm") ?? "Not clocked out").FontSize(7);
                        table.Cell().Padding(3).Text(r.MinutesLate.ToString()).FontSize(7);
                        table.Cell().Padding(3).Text(r.Status).FontSize(7);
                    }
                });

                page.Footer().AlignCenter().Text("Page").FontSize(10);
            });
        }).GeneratePdf(stream);

        return stream.ToArray();
    }

    public byte[] ToDocx(List<AttendanceExportDto> records)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            var document = new W.Document();
            var body = new W.Body();

            // Title
            body.AppendChild(new W.Paragraph(
                new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
                new W.Run(new W.RunProperties(new W.Bold(), new W.FontSize { Val = "32" }), new W.Text("HRMS Attendance Report"))
            ));

            // Generated date
            body.AppendChild(new W.Paragraph(
                new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
                new W.Run(new W.RunProperties(new W.FontSize { Val = "20" }), new W.Text($"Generated on {DateTime.Now:dd MMM yyyy HH:mm}"))
            ));

            body.AppendChild(new W.Paragraph(new W.Run(new W.Text(" "))));

            // Create table
            var table = new W.Table();
            table.AppendChild(new W.TableProperties(
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 6 },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 6 },
                    new W.LeftBorder { Val = W.BorderValues.Single, Size = 6 },
                    new W.RightBorder { Val = W.BorderValues.Single, Size = 6 },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 6 },
                    new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 6 }
                )));

            // Header row
            var headers = new[] { "Employee", "Branch", "Department", "Role", "Shift", "Date", "Start", "End", "Clock In", "Clock Out", "Minutes Late", "Status" };
            var headerRow = new W.TableRow();
            foreach (var h in headers)
                headerRow.AppendChild(CreateCell(h, bold: true));
            table.AppendChild(headerRow);

            // Data rows
            foreach (var r in records)
            {
                var row = new W.TableRow();
                row.AppendChild(CreateCell(r.EmployeeName));
                row.AppendChild(CreateCell(r.Branch));
                row.AppendChild(CreateCell(r.Department));
                row.AppendChild(CreateCell(r.Role));
                row.AppendChild(CreateCell(r.Shift));
                row.AppendChild(CreateCell(r.Date.ToString("dd/MM/yyyy")));
                row.AppendChild(CreateCell(r.StartTime.ToString("HH:mm")));
                row.AppendChild(CreateCell(r.EndTime.ToString("HH:mm")));
                row.AppendChild(CreateCell(r.ClockIn?.ToString("HH:mm") ?? "-"));
                row.AppendChild(CreateCell(r.ClockOut?.ToString("HH:mm") ?? "Not clocked out"));
                row.AppendChild(CreateCell(r.MinutesLate.ToString()));
                row.AppendChild(CreateCell(r.Status));
                table.AppendChild(row);
            }

            body.AppendChild(table);
            document.AppendChild(body);
            mainPart.Document = document;
        }
        return stream.ToArray();
    }

    private W.TableCell CreateCell(string text, bool bold = false)
    {
        var textElement = new W.Text(text);
        textElement.Space = SpaceProcessingModeValues.Preserve;
        var run = new W.Run(textElement);
        if (bold) run.RunProperties = new W.RunProperties(new W.Bold());
        return new W.TableCell(new W.Paragraph(run));
    }
}