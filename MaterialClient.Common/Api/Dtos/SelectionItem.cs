namespace MaterialClient.Common.Api.Dtos;

/// <summary>
/// 统一选择项模型，供可创建/可分页/可搜索选择控件与 ViewModel 交互使用。
/// </summary>
public sealed class SelectionItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
