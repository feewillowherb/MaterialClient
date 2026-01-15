using System.Runtime.Versioning;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services.Hardware;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests;

/// <summary>
/// Ticket Printing Service Tests
/// Tests for PDF and LQ-630K printer functionality.
/// All tests are marked as manual-only since they require printer hardware or PDF printer.
/// </summary>
[SupportedOSPlatform("windows")]
public class TicketPrintingServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly TicketPrintingService _service;

    public TicketPrintingServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _service = new TicketPrintingService();
    }

    #region DTO Creation Tests

    [Fact(Skip = "manual-only")]
    public void Test_CreateSampleDto()
    {
        // Act
        var dto = WeighingTicketDto.CreateSample();

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("杭州萧山城市运营管理有限公司", dto.CompanyName);
        Assert.Equal("东部资源化处置点称重计量单", dto.DocumentTitle);
        Assert.Equal("浙A8V676", dto.VehicleNumber);
        Assert.Equal("装修垃圾", dto.GoodsName);
        Assert.Equal(16030m, dto.GrossWeight);
        Assert.Equal(7610m, dto.TareWeight);
        Assert.Equal(8420m, dto.NetWeight);
        Assert.Equal("A202510310006", dto.SerialNumber);

        _output.WriteLine("Sample DTO created and validated successfully");
        _output.WriteLine($"  Vehicle: {dto.VehicleNumber}");
        _output.WriteLine($"  Gross Weight: {dto.GrossWeight} kg");
        _output.WriteLine($"  Tare Weight: {dto.TareWeight} kg");
        _output.WriteLine($"  Net Weight: {dto.NetWeight} kg");
    }

    [Fact(Skip = "manual-only")]
    public void Test_CreateDto_DefaultValues()
    {
        // Act
        var dto = new WeighingTicketDto();

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("杭州萧山城市运营管理有限公司", dto.CompanyName);
        Assert.Equal("东部资源化处置点称重计量单", dto.DocumentTitle);
        Assert.Equal("公斤", dto.MeasurementUnit);
        Assert.Equal(string.Empty, dto.VehicleNumber);
        Assert.Equal(0m, dto.GrossWeight);

        _output.WriteLine("Default DTO values validated successfully");
    }

    #endregion

    #region Printer Detection Tests

    [Fact(Skip = "manual-only")]
    public void Test_ListInstalledPrinters()
    {
        // Act
        var printers = _service.ListInstalledPrinters();

        // Assert
        Assert.NotNull(printers);
        _output.WriteLine($"Found {printers.Count} installed printers:");
        foreach (var printer in printers)
        {
            _output.WriteLine($"  - {printer}");
        }
    }

    [Fact(Skip = "manual-only")]
    public void Test_FindEpsonPrinter()
    {
        // Act
        var epsonPrinter = _service.FindEpsonPrinter();

        // Log results
        var installedPrinters = _service.ListInstalledPrinters();
        _output.WriteLine("Installed printers:");
        foreach (var printer in installedPrinters)
        {
            _output.WriteLine($"  - {printer}");
        }

        if (epsonPrinter != null)
        {
            _output.WriteLine($"\nEPSON LQ-630K found: {epsonPrinter}");
            Assert.Contains("EPSON", epsonPrinter, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            _output.WriteLine("\nEPSON LQ-630K not found in installed printers");
        }
    }

    [Fact(Skip = "manual-only")]
    public void Test_VerifyPdfPrinterAvailable()
    {
        // Arrange & Act
        var installedPrinters = _service.ListInstalledPrinters();
        var pdfPrinter = installedPrinters.FirstOrDefault(p =>
            p.Contains("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase));

        // Assert
        if (pdfPrinter != null)
        {
            _output.WriteLine($"Microsoft Print to PDF found: {pdfPrinter}");
        }
        else
        {
            _output.WriteLine("Microsoft Print to PDF not found in installed printers:");
            foreach (var printer in installedPrinters)
            {
                _output.WriteLine($"  - {printer}");
            }
        }
    }

    #endregion

    #region PDF Printing Tests

    [Fact(Skip = "manual-only")]
    public void Test_PrintTicketToPdf()
    {
        // Arrange
        var dto = WeighingTicketDto.CreateSample();
        var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
        var outputPdfPath = Path.Combine(outputDir, $"ticket_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        // Act
        try
        {
            var resultPath = _service.PrintToPdf(dto, outputPdfPath);

            // Assert
            Assert.True(File.Exists(resultPath), $"PDF file should exist at {resultPath}");
            var fileInfo = new FileInfo(resultPath);
            Assert.True(fileInfo.Length > 0, "PDF file should not be empty");

            _output.WriteLine($"PDF generated successfully:");
            _output.WriteLine($"  Path: {resultPath}");
            _output.WriteLine($"  Size: {fileInfo.Length} bytes");
            _output.WriteLine($"  Full path: {Path.GetFullPath(resultPath)}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Microsoft Print to PDF"))
        {
            _output.WriteLine($"Microsoft Print to PDF not available: {ex.Message}");
        }
    }

    [Fact(Skip = "manual-only")]
    public void Test_PrintImageToPdf()
    {
        // Arrange
        var sampleImagePath = Path.Combine(AppContext.BaseDirectory, "sample.png");
        if (!File.Exists(sampleImagePath))
        {
            _output.WriteLine($"Sample image not found at {sampleImagePath} - test skipped");
            return;
        }

        var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
        var outputPdfPath = Path.Combine(outputDir, $"sample_image_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        // Act
        try
        {
            var resultPath = _service.PrintImageToPdf(sampleImagePath, outputPdfPath);

            // Assert
            Assert.True(File.Exists(resultPath), $"PDF file should exist at {resultPath}");
            var fileInfo = new FileInfo(resultPath);
            Assert.True(fileInfo.Length > 0, "PDF file should not be empty");

            _output.WriteLine($"PDF generated from image successfully:");
            _output.WriteLine($"  Source: {sampleImagePath}");
            _output.WriteLine($"  Output: {resultPath}");
            _output.WriteLine($"  Size: {fileInfo.Length} bytes");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Microsoft Print to PDF"))
        {
            _output.WriteLine($"Microsoft Print to PDF not available: {ex.Message}");
        }
    }

    [Fact(Skip = "manual-only")]
    public void Test_PrintToPdf_CreatesOutputDirectory()
    {
        // Arrange
        var dto = WeighingTicketDto.CreateSample();
        var outputDir = Path.Combine(AppContext.BaseDirectory, "output", "nested", "directory");
        var outputPdfPath = Path.Combine(outputDir, $"ticket_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        // Ensure directory doesn't exist
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        // Act
        try
        {
            var resultPath = _service.PrintToPdf(dto, outputPdfPath);

            // Assert
            Assert.True(Directory.Exists(outputDir), "Output directory should be created");
            Assert.True(File.Exists(resultPath), "PDF file should exist");

            _output.WriteLine($"PDF generated with nested directory creation:");
            _output.WriteLine($"  Directory: {outputDir}");
            _output.WriteLine($"  File: {resultPath}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Microsoft Print to PDF"))
        {
            _output.WriteLine($"Microsoft Print to PDF not available: {ex.Message}");
        }
    }

    #endregion

    #region LQ-630K Printer Tests

    [Fact(Skip = "manual-only")]
    public void Test_PrintToEpsonLq630K()
    {
        // Arrange
        var dto = WeighingTicketDto.CreateSample();
        var printer = _service.FindEpsonPrinter();

        if (printer == null)
        {
            _output.WriteLine("EPSON LQ-630K printer not found. Please ensure the printer is connected and installed.");
            return;
        }

        // Act
        _output.WriteLine($"Printing ticket to {printer}...");
        _service.PrintToEpsonLq630K(dto, printer);

        _output.WriteLine("Print job sent successfully");
    }

    [Fact(Skip = "manual-only")]
    public void Test_PrintImageToEpsonLq630K()
    {
        // Arrange
        var sampleImagePath = Path.Combine(AppContext.BaseDirectory, "sample.png");
        if (!File.Exists(sampleImagePath))
        {
            _output.WriteLine($"Sample image not found at {sampleImagePath} - test skipped");
            return;
        }

        var printer = _service.FindEpsonPrinter();
        if (printer == null)
        {
            _output.WriteLine("EPSON LQ-630K printer not found");
            return;
        }

        // Act
        _output.WriteLine($"Printing image to {printer}...");
        _service.PrintImage(sampleImagePath, printer);

        _output.WriteLine("Image print job sent successfully");
    }

    [Fact(Skip = "manual-only")]
    public void Test_PrintToEpsonLq630K_InvalidPrinter()
    {
        // Arrange
        var dto = WeighingTicketDto.CreateSample();
        var invalidPrinterName = "NonExistent Printer XYZ123";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.PrintToEpsonLq630K(dto, invalidPrinterName));

        Assert.Contains("not valid or not installed", exception.Message);

        _output.WriteLine($"Expected exception thrown: {exception.Message}");
    }

    #endregion

    #region Error Handling Tests

    [Fact(Skip = "manual-only")]
    public void Test_PrintImageToPdf_FileNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(AppContext.BaseDirectory, "nonexistent_image.png");
        var outputPdfPath = Path.Combine(AppContext.BaseDirectory, "output", "test.pdf");

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
            _service.PrintImageToPdf(nonExistentPath, outputPdfPath));

        Assert.Contains("Image file not found", exception.Message);

        _output.WriteLine($"Expected exception thrown: {exception.Message}");
    }

    [Fact(Skip = "manual-only")]
    public void Test_PrintImage_FileNotFound()
    {
        // Arrange
        var nonExistentPath = Path.Combine(AppContext.BaseDirectory, "nonexistent_image.png");

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
            _service.PrintImage(nonExistentPath));

        Assert.Contains("Image file not found", exception.Message);

        _output.WriteLine($"Expected exception thrown: {exception.Message}");
    }

    #endregion
}
