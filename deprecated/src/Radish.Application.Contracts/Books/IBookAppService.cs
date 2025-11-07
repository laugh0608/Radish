using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Radish.Books;

/// <summary>
/// 书籍管理应用服务（CRUD）。
/// </summary>
/// <remarks>
/// - 支持分页排序查询、按主键查询
/// - 创建/更新/删除书籍
/// - 需要相应的权限（参见 RadishPermissions.Books）
/// </remarks>
public interface IBookAppService :
    ICrudAppService< //Defines CRUD methods
        BookDto, //Used to show books
        Guid, //Primary key of the book entity
        PagedAndSortedResultRequestDto, //Used for paging/sorting
        CreateUpdateBookDto> //Used to create/update a book
{

}
