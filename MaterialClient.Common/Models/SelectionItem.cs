namespace MaterialClient.Common.Models;

/// <summary>
/// 统一的选择项包装类，包含Id和Name属性
/// </summary>
public class SelectionItem : IEquatable<SelectionItem>
{
    public int Id { get; }
    public string Name { get; }

    public SelectionItem(int id, string name)
    {
        Id = id;
        Name = name ?? string.Empty;
    }

    public bool Equals(SelectionItem? other)
    {
        if (other == null) return false;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as SelectionItem);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
