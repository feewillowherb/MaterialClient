using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Csv;
using MaterialClientToolkit.Models;
using Volo.Abp.DependencyInjection;

namespace MaterialClientToolkit.Services;

/// <summary>
/// CSV读取服务
/// 参考: https://github.com/dotnetcore/Magicodes.IE/blob/master/docs/7.Csv%20Import%20and%20Export.md
/// </summary>
public class CsvReaderService : ITransientDependency
{
    /// <summary>
    /// 读取Material_Order.csv文件
    /// </summary>
    public async Task<List<MaterialOrderCsv>> ReadMaterialOrderAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV文件不存在: {filePath}");

        var csvImporter = new CsvImporter();

        // 使用Stream方式导入
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var result = await csvImporter.Import<MaterialOrderCsv>(stream);

        if (!result.HasError && result.Data != null)
            return result.Data.ToList();

        // 处理错误信息
        var errorMessage = "CSV导入过程中发生错误，请检查数据格式";
        if (result.HasError && result.TemplateErrors != null && result.TemplateErrors.Any())
        {
            errorMessage = string.Join("; ", result.TemplateErrors);
        }

        throw new InvalidOperationException($"读取CSV文件失败: {errorMessage}");
    }

    /// <summary>
    /// 读取Material_OrderGoods.csv文件
    /// </summary>
    public async Task<List<MaterialOrderGoodsCsv>> ReadMaterialOrderGoodsAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV文件不存在: {filePath}");

        var csvImporter = new CsvImporter();

        // 使用Stream方式导入
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var result = await csvImporter.Import<MaterialOrderGoodsCsv>(stream);

        if (!result.HasError && result.Data != null)
            return result.Data.ToList();

        // 处理错误信息
        var errorMessage = "CSV导入过程中发生错误，请检查数据格式";
        if (result.HasError && result.TemplateErrors != null && result.TemplateErrors.Any())
        {
            errorMessage = string.Join("; ", result.TemplateErrors);
        }

        throw new InvalidOperationException($"读取CSV文件失败: {errorMessage}");
    }

    /// <summary>
    /// 读取Material_Attaches.csv文件
    /// </summary>
    public async Task<List<MaterialAttachesCsv>> ReadMaterialAttachesAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV文件不存在: {filePath}");

        var csvImporter = new CsvImporter();

        // 使用Stream方式导入
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var result = await csvImporter.Import<MaterialAttachesCsv>(stream);

        if (!result.HasError && result.Data != null)
            return result.Data.ToList();

        // 处理错误信息
        var errorMessage = "CSV导入过程中发生错误，请检查数据格式";
        if (result.HasError && result.TemplateErrors != null && result.TemplateErrors.Any())
        {
            errorMessage = string.Join("; ", result.TemplateErrors);
        }

        throw new InvalidOperationException($"读取CSV文件失败: {errorMessage}");
    }
}