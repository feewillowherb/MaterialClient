using Magicodes.ExporterAndImporter.Csv;
using MaterialClientToolkit.Models;

namespace MaterialClientToolkit.Services;

/// <summary>
/// CSV读取服务
/// </summary>
public class CsvReaderService
{
    private readonly ICsvImporter _csvImporter;

    public CsvReaderService()
    {
        _csvImporter = new CsvImporter();
    }

    /// <summary>
    /// 读取Material_Order.csv文件
    /// </summary>
    public async Task<List<MaterialOrderCsv>> ReadMaterialOrderAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV文件不存在: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var result = await _csvImporter.Import<MaterialOrderCsv>(stream);
        
        if (!result.HasError)
            return result.Data.ToList();

        var errors = string.Join("; ", result.ExceptionList.Select(e => e.ErrorMessage));
        throw new InvalidOperationException($"读取CSV文件失败: {errors}");
    }

    /// <summary>
    /// 读取Material_OrderGoods.csv文件
    /// </summary>
    public async Task<List<MaterialOrderGoodsCsv>> ReadMaterialOrderGoodsAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV文件不存在: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var result = await _csvImporter.Import<MaterialOrderGoodsCsv>(stream);
        
        if (!result.HasError)
            return result.Data.ToList();

        var errors = string.Join("; ", result.ExceptionList.Select(e => e.ErrorMessage));
        throw new InvalidOperationException($"读取CSV文件失败: {errors}");
    }

    /// <summary>
    /// 读取Material_Attaches.csv文件
    /// </summary>
    public async Task<List<MaterialAttachesCsv>> ReadMaterialAttachesAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV文件不存在: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var result = await _csvImporter.Import<MaterialAttachesCsv>(stream);
        
        if (!result.HasError)
            return result.Data.ToList();

        var errors = string.Join("; ", result.ExceptionList.Select(e => e.ErrorMessage));
        throw new InvalidOperationException($"读取CSV文件失败: {errors}");
    }
}

