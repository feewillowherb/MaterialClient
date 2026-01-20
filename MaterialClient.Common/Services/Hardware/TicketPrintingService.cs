using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.Runtime.Versioning;
using MaterialClient.Common.Models;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Interface for ticket printing service that supports PDF and physical printer output
/// </summary>
public interface ITicketPrintingService
{
    /// <summary>
    /// Print weighing ticket to PDF file using Microsoft Print to PDF
    /// </summary>
    /// <param name="dto">Ticket data to print</param>
    /// <param name="outputPdfPath">Path where the PDF file should be saved</param>
    /// <returns>Path to the generated PDF file</returns>
    string PrintToPdf(WeighingTicketDto dto, string outputPdfPath);

    /// <summary>
    /// Print weighing ticket to EPSON LQ-630K dot matrix printer
    /// </summary>
    /// <param name="dto">Ticket data to print</param>
    /// <param name="printerName">Optional printer name override. If null, auto-detects LQ-630K</param>
    void PrintToEpsonLq630K(WeighingTicketDto dto, string? printerName = null);

    /// <summary>
    /// Find the EPSON LQ-630K printer in the system
    /// </summary>
    /// <returns>Printer name if found, null otherwise</returns>
    string? FindEpsonPrinter();

    /// <summary>
    /// List all installed printers in the system
    /// </summary>
    /// <returns>List of printer names</returns>
    List<string> ListInstalledPrinters();

    /// <summary>
    /// Print an image file to PDF using Microsoft Print to PDF
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="outputPdfPath">Path where the PDF file should be saved</param>
    /// <returns>Path to the generated PDF file</returns>
    string PrintImageToPdf(string imagePath, string outputPdfPath);

    /// <summary>
    /// Print an image file directly to a printer
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="printerName">Optional printer name. If null, uses default or auto-detected printer</param>
    void PrintImage(string imagePath, string? printerName = null);

    /// <summary>
    /// Render weighing ticket to an image file (PNG) for preview.
    /// </summary>
    /// <param name="dto">Ticket data to render</param>
    /// <param name="outputImagePath">Full path to output image (PNG)</param>
    /// <returns>Path to the generated image file</returns>
    string RenderTicketToImage(WeighingTicketDto dto, string outputImagePath);
}

/// <summary>
/// Ticket printing service implementation
/// Supports printing weighing tickets to PDF and EPSON LQ-630K dot matrix printer
/// </summary>
[SupportedOSPlatform("windows")]
public class TicketPrintingService : ITicketPrintingService, ISingletonDependency
{
    private const string DefaultEpsonPrinterName = "EPSON LQ-630K";
    private const string PdfPrinterName = "Microsoft Print to PDF";

    private readonly ILogger<TicketPrintingService>? _logger;

    public TicketPrintingService(ILogger<TicketPrintingService>? logger = null)
    {
        _logger = logger;
    }

    #region Printer Detection

    /// <summary>
    /// Find the EPSON LQ-630K printer in the system
    /// </summary>
    /// <returns>Printer name if found, null otherwise</returns>
    public string? FindEpsonPrinter()
    {
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            _logger?.LogDebug("Found printer: {PrinterName}", printer);
            if (printer.Contains("EPSON", StringComparison.OrdinalIgnoreCase) &&
                printer.Contains("LQ-630K", StringComparison.OrdinalIgnoreCase))
            {
                return printer;
            }
        }

        // Also check for partial matches
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            if (printer.Contains("LQ-630", StringComparison.OrdinalIgnoreCase) ||
                printer.Contains("EPSON LQ", StringComparison.OrdinalIgnoreCase))
            {
                return printer;
            }
        }

        return null;
    }

    /// <summary>
    /// List all installed printers in the system
    /// </summary>
    /// <returns>List of printer names</returns>
    public List<string> ListInstalledPrinters()
    {
        var printers = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            printers.Add(printer);
        }

        return printers;
    }

    #endregion

    #region PDF Printing

    /// <summary>
    /// Print weighing ticket to PDF file using Microsoft Print to PDF
    /// </summary>
    /// <param name="dto">Ticket data to print</param>
    /// <param name="outputPdfPath">Path where the PDF file should be saved</param>
    /// <returns>Path to the generated PDF file</returns>
    public string PrintToPdf(WeighingTicketDto dto, string outputPdfPath)
    {
        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPdfPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        _logger?.LogInformation("Printing ticket to PDF: {OutputPath}", outputPdfPath);

        var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = PdfPrinterName;
        printDocument.PrinterSettings.PrintToFile = true;
        printDocument.PrinterSettings.PrintFileName = outputPdfPath;

        if (!printDocument.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException(
                $"PDF printer '{PdfPrinterName}' is not available. " +
                "Please ensure Microsoft Print to PDF is installed on your system.");
        }

        printDocument.PrintPage += (_, e) => DrawTicket(e, dto);

        printDocument.Print();
        _logger?.LogInformation("PDF file generated successfully: {OutputPath}", outputPdfPath);

        return outputPdfPath;
    }

    /// <summary>
    /// Print an image file to PDF using Microsoft Print to PDF
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="outputPdfPath">Path where the PDF file should be saved</param>
    /// <returns>Path to the generated PDF file</returns>
    public string PrintImageToPdf(string imagePath, string outputPdfPath)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Image file not found: {imagePath}");
        }

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPdfPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var image = Image.FromFile(imagePath);
        Image? capturedImage = image;

        var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = PdfPrinterName;
        printDocument.PrinterSettings.PrintToFile = true;
        printDocument.PrinterSettings.PrintFileName = outputPdfPath;

        if (!printDocument.PrinterSettings.IsValid)
        {
            image.Dispose();
            throw new InvalidOperationException(
                $"PDF printer '{PdfPrinterName}' is not available. " +
                "Please ensure Microsoft Print to PDF is installed on your system.");
        }

        printDocument.PrintPage += (_, e) =>
        {
            if (capturedImage != null)
            {
                DrawImage(e, capturedImage);
                capturedImage.Dispose();
                capturedImage = null;
            }
        };

        printDocument.Print();
        _logger?.LogInformation("PDF file generated from image successfully: {OutputPath}", outputPdfPath);

        return outputPdfPath;
    }

    #endregion

    #region Preview Rendering

    /// <summary>
    /// Render weighing ticket to an image file (PNG) for preview.
    /// </summary>
    public string RenderTicketToImage(WeighingTicketDto dto, string outputImagePath)
    {
        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputImagePath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // A4-ish canvas at ~150 DPI (good enough for preview).
        const int width = 1240;
        const int height = 1754;

        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.White);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Reuse existing DrawTicket() by providing a PrintPageEventArgs with margins.
        var pageBounds = new Rectangle(0, 0, width, height);
        var marginBounds = new Rectangle(40, 40, width - 80, height - 80);
        var pageSettings = new PageSettings();
        var args = new PrintPageEventArgs(graphics, marginBounds, pageBounds, pageSettings);

        DrawTicket(args, dto);

        bitmap.Save(outputImagePath, ImageFormat.Png);
        _logger?.LogInformation("Ticket preview image generated: {OutputPath}", outputImagePath);

        return outputImagePath;
    }

    #endregion

    #region Physical Printer Printing

    /// <summary>
    /// Print weighing ticket to EPSON LQ-630K dot matrix printer
    /// </summary>
    /// <param name="dto">Ticket data to print</param>
    /// <param name="printerName">Optional printer name override. If null, auto-detects LQ-630K</param>
    public void PrintToEpsonLq630K(WeighingTicketDto dto, string? printerName = null)
    {
        var targetPrinter = printerName ?? FindEpsonPrinter() ?? DefaultEpsonPrinterName;

        _logger?.LogInformation("Printing ticket to: {PrinterName}", targetPrinter);

        var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = targetPrinter;

        if (!printDocument.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{targetPrinter}' is not valid or not installed.");
        }

        printDocument.PrintPage += (_, e) => DrawTicket(e, dto);

        printDocument.Print();
        _logger?.LogInformation("Print job sent successfully to {PrinterName}", targetPrinter);
    }

    /// <summary>
    /// Print an image file directly to a printer
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="printerName">Optional printer name. If null, uses default or auto-detected printer</param>
    public void PrintImage(string imagePath, string? printerName = null)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Image file not found: {imagePath}");
        }

        var targetPrinter = printerName ?? FindEpsonPrinter() ?? DefaultEpsonPrinterName;

        var image = Image.FromFile(imagePath);
        Image? capturedImage = image;

        var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = targetPrinter;

        if (!printDocument.PrinterSettings.IsValid)
        {
            image.Dispose();
            throw new InvalidOperationException($"Printer '{targetPrinter}' is not valid or not installed.");
        }

        printDocument.PrintPage += (_, e) =>
        {
            if (capturedImage != null)
            {
                DrawImage(e, capturedImage);
                capturedImage.Dispose();
                capturedImage = null;
            }
        };

        printDocument.Print();
        _logger?.LogInformation("Image print job sent successfully to {PrinterName}", targetPrinter);
    }

    #endregion

    #region Drawing Methods

    /// <summary>
    /// Draw the ticket content on the graphics surface
    /// </summary>
    private void DrawTicket(PrintPageEventArgs e, WeighingTicketDto dto)
    {
        if (e.Graphics == null) return;

        var graphics = e.Graphics;

        // Define fonts for different elements
        using var titleFont = new Font("SimHei", 14, FontStyle.Bold);
        using var headerFont = new Font("SimSun", 12, FontStyle.Bold);
        using var normalFont = new Font("SimSun", 10);
        using var smallFont = new Font("SimSun", 8);

        var brush = Brushes.Black;
        float y = 20;
        
        // Use MarginBounds to ensure content fits within printable area
        // This is critical for physical printers like EPSON LQ-630K which have smaller printable areas
        float leftMargin = e.MarginBounds.Left;
        float rightMargin = e.MarginBounds.Right;
        float printableWidth = e.MarginBounds.Width;
        float centerX = e.MarginBounds.Left + printableWidth / 2;
        float lineHeight = 25;

        // Draw company name (centered)
        var companySize = graphics.MeasureString(dto.CompanyName, titleFont);
        graphics.DrawString(dto.CompanyName, titleFont, brush,
            centerX - companySize.Width / 2, y);
        y += lineHeight + 5;

        // Draw document title (centered) - using same font style as CompanyName
        var titleSize = graphics.MeasureString(dto.DocumentTitle, titleFont);
        graphics.DrawString(dto.DocumentTitle, titleFont, brush,
            centerX - titleSize.Width / 2, y);
        y += lineHeight + 10;

        // Draw header info line - adjust positions to fit within printable area
        float headerCol2 = leftMargin + printableWidth * 0.35f;
        float headerCol3 = leftMargin + printableWidth * 0.65f;
        graphics.DrawString($"打印时间: {dto.PrintTime:yyyy-MM-dd HH:mm:ss}", smallFont, brush, leftMargin, y);
        graphics.DrawString($"流水号: {dto.SerialNumber}", smallFont, brush, headerCol2, y);
        graphics.DrawString($"计量单位: {dto.MeasurementUnit}", smallFont, brush, headerCol3, y);
        y += lineHeight + 10;

        // Define table structure with columns
        // Table has 4 columns: Label1 | Value1 | Label2 | Value2
        // Based on sample.png: label columns are ~38% width, value columns are ~62% width
        float tableWidth = printableWidth;
        float sectionWidth = tableWidth / 2; // Each section (left/right) takes half the table width
        float labelWidth = sectionWidth * 0.38f; // Label column is 38% of section width
        float valueWidth = sectionWidth * 0.62f; // Value column is 62% of section width

        float col1 = leftMargin; // Left column labels start
        float col2 = col1 + labelWidth; // Left column values start
        float col3 = col2 + valueWidth; // Right column labels start
        float col4 = col3 + labelWidth; // Right column values start
        float tableRight = rightMargin; // Use right margin bound for rightmost border
        float tableTop = y;

        // Draw table content with grid lines
        float currentY = y;

        // Row 1
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "车号", dto.VehicleNumber, "收货单位", dto.ReceivingUnit, normalFont, brush);
        currentY += lineHeight;

        // Row 2
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "货名", dto.GoodsName, "毛重", dto.GrossWeight.ToString(CultureInfo.InvariantCulture), normalFont, brush);
        currentY += lineHeight;

        // Row 3
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "发货单位", dto.ShippingUnit, "皮重", dto.TareWeight.ToString(CultureInfo.InvariantCulture), normalFont, brush);
        currentY += lineHeight;

        // Row 4
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "进场时间", dto.EntryTime.ToString("yyyy-MM-dd HH:mm:ss"), "净重", dto.NetWeight.ToString(CultureInfo.InvariantCulture), normalFont,
            brush);
        currentY += lineHeight;

        // Row 5
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "出场时间", dto.ExitTime.ToString("yyyy-MM-dd HH:mm:ss"), "类型", dto.Type, normalFont, brush);
        currentY += lineHeight;

        // Row 6
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "备注", dto.Remarks, "联单编号", dto.ManifestNumber, normalFont, brush);
        currentY += lineHeight;

        // Row 7 - Signatures
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "司磅员签字", dto.WeigherSignature, "所属镇街", dto.TownStreet, normalFont, brush);
        currentY += lineHeight;

        // Row 8
        DrawTableRow(graphics, Pens.Black, col1, col2, col3, col4, tableRight, currentY, lineHeight,
            "驾驶员签字", dto.DriverSignature, "监磅员签字", dto.SupervisorSignature, normalFont, brush);
        currentY += lineHeight;

        // Draw table grid lines (vertical and horizontal)
        DrawTableGrid(graphics, Pens.Black, col1, col2, col3, col4, tableRight, tableTop, currentY);
    }

    /// <summary>
    /// Draw a table row with text content and horizontal line
    /// All text is center-aligned within cells
    /// </summary>
    private void DrawTableRow(Graphics graphics, Pen pen, float col1, float col2, float col3, float col4,
        float tableRight, float y, float lineHeight, string label1, string value1, string label2, string value2,
        Font font, Brush brush)
    {
        // Calculate column widths for centering
        float labelWidth = col2 - col1; // Width of label column
        float valueWidth = col3 - col2; // Width of value column (left section)
        float label2Width = col4 - col3; // Width of label column (right section)
        float value2Width = tableRight - col4; // Width of value column (right section)

        // Center text horizontally within each cell
        var label1Size = graphics.MeasureString(label1, font);
        var value1Size = graphics.MeasureString(value1, font);
        var label2Size = graphics.MeasureString(label2, font);
        var value2Size = graphics.MeasureString(value2, font);

        // Calculate vertical center position
        float textY = y + (lineHeight - label1Size.Height) / 2;

        // Draw text centered in each cell
        graphics.DrawString(label1, font, brush, col1 + (labelWidth - label1Size.Width) / 2, textY);
        graphics.DrawString(value1, font, brush, col2 + (valueWidth - value1Size.Width) / 2, textY);
        graphics.DrawString(label2, font, brush, col3 + (label2Width - label2Size.Width) / 2, textY);
        graphics.DrawString(value2, font, brush, col4 + (value2Width - value2Size.Width) / 2, textY);

        // Draw horizontal line at bottom of row
        graphics.DrawLine(pen, col1, y + lineHeight, tableRight, y + lineHeight);
    }

    /// <summary>
    /// Draw table grid lines (vertical and horizontal)
    /// </summary>
    private void DrawTableGrid(Graphics graphics, Pen pen, float col1, float col2, float col3, float col4,
        float tableRight, float tableTop, float tableBottom)
    {
        // Draw vertical lines (column separators)
        float tableLeft = col1;

        // Left border
        graphics.DrawLine(pen, tableLeft, tableTop, tableLeft, tableBottom);

        // Column separator 1 (between label1 and value1)
        graphics.DrawLine(pen, col2, tableTop, col2, tableBottom);

        // Column separator 2 (between value1 and label2)
        graphics.DrawLine(pen, col3, tableTop, col3, tableBottom);

        // Column separator 3 (between label2 and value2)
        graphics.DrawLine(pen, col4, tableTop, col4, tableBottom);

        // Right border
        graphics.DrawLine(pen, tableRight, tableTop, tableRight, tableBottom);

        // Draw top border (horizontal line)
        graphics.DrawLine(pen, tableLeft, tableTop, tableRight, tableTop);
    }

    /// <summary>
    /// Draw image on the graphics surface
    /// </summary>
    private void DrawImage(PrintPageEventArgs e, Image image)
    {
        if (e.Graphics == null) return;

        // Configure rendering to preserve table lines and details
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        e.Graphics.SmoothingMode = SmoothingMode.None; // Disable smoothing for sharp lines

        // Calculate scale to fit page while maintaining aspect ratio
        var pageWidth = e.PageBounds.Width - 40;
        var pageHeight = e.PageBounds.Height - 40;

        var scale = Math.Min(pageWidth / (float)image.Width, pageHeight / (float)image.Height);

        // If scale is close to 1.0, use original size to preserve all details
        if (scale > 0.95f && scale < 1.05f)
        {
            scale = 1.0f; // Use original size
        }

        var width = (int)(image.Width * scale);
        var height = (int)(image.Height * scale);

        var x = (e.PageBounds.Width - width) / 2;
        var y = 20;

        // Draw image with settings optimized for table lines
        var destRect = new RectangleF(x, y, width, height);
        var srcRect = new RectangleF(0, 0, image.Width, image.Height);

        e.Graphics.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);
    }

    #endregion
}
