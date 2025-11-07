using System;
using Volo.Abp.Application.Dtos;

namespace Radish.Books;

/// <summary>
/// 书籍数据传输对象。
/// </summary>
public class BookDto : AuditedEntityDto<Guid>
{
    /// <summary>
    /// 书名。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 书籍类型。
    /// </summary>
    public BookType Type { get; set; }

    /// <summary>
    /// 出版日期。
    /// </summary>
    public DateTime PublishDate { get; set; }

    /// <summary>
    /// 价格。
    /// </summary>
    public float Price { get; set; }
}
