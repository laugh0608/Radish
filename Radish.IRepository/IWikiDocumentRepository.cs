using System.Linq.Expressions;
using Radish.IRepository.Base;
using Radish.Model;
using SqlSugar;

namespace Radish.IRepository;

/// <summary>Wiki 文档仓储接口</summary>
public interface IWikiDocumentRepository : IBaseRepository<WikiDocument>
{
    /// <summary>根据 ID 查询文档（包含已删除数据）</summary>
    Task<WikiDocument?> QueryByIdIncludingDeletedAsync(long id);

    /// <summary>分页查询文档（包含已删除数据）</summary>
    Task<(List<WikiDocument> data, int totalCount)> QueryPageIncludingDeletedAsync(
        Expression<Func<WikiDocument, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<WikiDocument, object>>? orderByExpression,
        OrderByType orderByType,
        Expression<Func<WikiDocument, object>>? thenByExpression,
        OrderByType thenByType);
}
