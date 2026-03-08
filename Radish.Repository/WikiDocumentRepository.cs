using System.Linq.Expressions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>Wiki 文档仓储</summary>
public class WikiDocumentRepository : BaseRepository<WikiDocument>, IWikiDocumentRepository
{
    public WikiDocumentRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<WikiDocument?> QueryByIdIncludingDeletedAsync(long id)
    {
        return await CreateTenantQueryableFor<WikiDocument>(includeDeleted: true).InSingleAsync(id);
    }

    public async Task<(List<WikiDocument> data, int totalCount)> QueryPageIncludingDeletedAsync(
        Expression<Func<WikiDocument, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<WikiDocument, object>>? orderByExpression,
        OrderByType orderByType,
        Expression<Func<WikiDocument, object>>? thenByExpression,
        OrderByType thenByType)
    {
        RefAsync<int> totalCount = 0;
        var query = CreateTenantQueryableFor<WikiDocument>(includeDeleted: true);
        if (whereExpression != null)
        {
            query = query.Where(whereExpression);
        }

        if (orderByExpression != null)
        {
            query = orderByType == OrderByType.Asc
                ? query.OrderBy(orderByExpression)
                : query.OrderByDescending(orderByExpression);
        }

        if (thenByExpression != null)
        {
            query = query.OrderBy(thenByExpression, thenByType);
        }

        var data = await query.ToPageListAsync(pageIndex, pageSize, totalCount);
        return (data, totalCount);
    }
}
