using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Radish.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;

namespace Radish.Books;

/// <summary>
/// 书籍管理服务。
/// </summary>
/// <remarks>
/// 提供书籍的增删改查等常见操作。
/// </remarks>
[Authorize(RadishPermissions.Books.Default)]
public class BookAppService : ApplicationService, IBookAppService
{
    private readonly IRepository<Book, Guid> _repository;

    public BookAppService(IRepository<Book, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 按书籍主键获取详情。
    /// </summary>
    /// <param name="id">书籍ID。</param>
    public async Task<BookDto> GetAsync(Guid id)
    {
        var book = await _repository.GetAsync(id);
        return ObjectMapper.Map<Book, BookDto>(book);
    }

    /// <summary>
    /// 分页获取书籍列表。
    /// </summary>
    /// <param name="input">分页与排序参数。</param>
    public async Task<PagedResultDto<BookDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "Name" : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var books = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        return new PagedResultDto<BookDto>(
            totalCount,
            ObjectMapper.Map<List<Book>, List<BookDto>>(books)
        );
    }

    /// <summary>
    /// 创建书籍。
    /// </summary>
    /// <param name="input">创建参数。</param>
    [Authorize(RadishPermissions.Books.Create)]
    public async Task<BookDto> CreateAsync(CreateUpdateBookDto input)
    {
        var book = ObjectMapper.Map<CreateUpdateBookDto, Book>(input);
        await _repository.InsertAsync(book);
        return ObjectMapper.Map<Book, BookDto>(book);
    }

    /// <summary>
    /// 更新书籍。
    /// </summary>
    /// <param name="id">书籍ID。</param>
    /// <param name="input">更新参数。</param>
    [Authorize(RadishPermissions.Books.Edit)]
    public async Task<BookDto> UpdateAsync(Guid id, CreateUpdateBookDto input)
    {
        var book = await _repository.GetAsync(id);
        ObjectMapper.Map(input, book);
        await _repository.UpdateAsync(book);
        return ObjectMapper.Map<Book, BookDto>(book);
    }

    /// <summary>
    /// 删除书籍。
    /// </summary>
    /// <param name="id">书籍ID。</param>
    [Authorize(RadishPermissions.Books.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
