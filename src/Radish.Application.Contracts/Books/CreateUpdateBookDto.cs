using System;
using System.ComponentModel.DataAnnotations;

namespace Radish.Books;

/// <summary>
/// 创建或更新书籍的参数。
/// </summary>
public class CreateUpdateBookDto
{
    /// <summary>
    /// 书名。
    /// </summary>
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 书籍类型。
    /// </summary>
    [Required]
    public BookType Type { get; set; } = BookType.Undefined;

    /// <summary>
    /// 出版日期。
    /// </summary>
    [Required]
    [DataType(DataType.Date)]
    public DateTime PublishDate { get; set; } = DateTime.Now;

    /// <summary>
    /// 价格。
    /// </summary>
    [Required]
    public float Price { get; set; }
}
