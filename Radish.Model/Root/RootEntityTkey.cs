using SqlSugar;

namespace Radish.Model.Root;

public class RootEntityTKey<TKey> where TKey : IEquatable<TKey>
{
    /// <summary>Id 泛型主键 Tkey</summary>
    [SugarColumn(IsNullable = false, IsPrimaryKey = true)]
    public TKey Id { get; set; }
}