using System.Linq.Expressions;
using AutoMapper;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using SqlSugar;

namespace Radish.Service;

/// <summary>
/// 审计日志服务实现
/// </summary>
public class AuditLogService : BaseService<AuditLog, AuditLogVo>, IAuditLogService
{
    private readonly IBaseRepository<AuditLog> _auditLogRepository;
    private readonly IMapper _mapper;

    public AuditLogService(
        IMapper mapper,
        IBaseRepository<AuditLog> auditLogRepository)
        : base(mapper, auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// 分页查询审计日志
    /// </summary>
    public async Task<PageModel<AuditLogVo>> QueryPageAsync(AuditLogQueryDto queryDto)
    {
        // 构建查询条件
        var whereExpression = Expressionable.Create<AuditLog>();

        if (queryDto.UserId.HasValue)
        {
            whereExpression.And(x => x.UserId == queryDto.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.UserName))
        {
            whereExpression.And(x => x.UserName != null && x.UserName.Contains(queryDto.UserName));
        }

        if (!string.IsNullOrWhiteSpace(queryDto.OperationType))
        {
            whereExpression.And(x => x.OperationType == queryDto.OperationType);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.Module))
        {
            whereExpression.And(x => x.Module == queryDto.Module);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.IpAddress))
        {
            whereExpression.And(x => x.IpAddress == queryDto.IpAddress);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.RequestPath))
        {
            whereExpression.And(x => x.RequestPath != null && x.RequestPath.Contains(queryDto.RequestPath));
        }

        if (queryDto.IsSuccess.HasValue)
        {
            whereExpression.And(x => x.IsSuccess == queryDto.IsSuccess.Value);
        }

        if (queryDto.StartTime.HasValue)
        {
            whereExpression.And(x => x.DateTime >= queryDto.StartTime.Value);
        }

        if (queryDto.EndTime.HasValue)
        {
            whereExpression.And(x => x.DateTime <= queryDto.EndTime.Value);
        }

        // 排序表达式
        Expression<Func<AuditLog, object>>? orderByExpression = queryDto.OrderBy?.ToLower() switch
        {
            "datetime" => x => x.DateTime,
            "userid" => x => x.UserId!,
            "operationtype" => x => x.OperationType,
            "duration" => x => x.Duration!,
            _ => x => x.DateTime
        };

        var orderDirection = queryDto.OrderDirection?.ToLower() == "asc" ? OrderByType.Asc : OrderByType.Desc;

        // 分页查询
        var (data, totalCount) = await _auditLogRepository.QueryPageAsync(
            whereExpression.ToExpression(),
            queryDto.PageIndex,
            queryDto.PageSize,
            orderByExpression,
            orderDirection);

        // 映射到 VO
        var voList = _mapper.Map<List<AuditLogVo>>(data);

        // 计算总页数
        var pageCount = (int)Math.Ceiling((double)totalCount / queryDto.PageSize);

        return new PageModel<AuditLogVo>
        {
            Page = queryDto.PageIndex,
            PageSize = queryDto.PageSize,
            DataCount = totalCount,
            PageCount = pageCount,
            Data = voList
        };
    }

    /// <summary>
    /// 根据用户 ID 查询审计日志
    /// </summary>
    public async Task<PageModel<AuditLogVo>> QueryByUserIdAsync(long userId, int pageIndex = 1, int pageSize = 20)
    {
        var queryDto = new AuditLogQueryDto
        {
            UserId = userId,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        return await QueryPageAsync(queryDto);
    }

    /// <summary>
    /// 根据操作类型查询审计日志
    /// </summary>
    public async Task<PageModel<AuditLogVo>> QueryByOperationTypeAsync(string operationType, int pageIndex = 1, int pageSize = 20)
    {
        var queryDto = new AuditLogQueryDto
        {
            OperationType = operationType,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        return await QueryPageAsync(queryDto);
    }

    /// <summary>
    /// 根据时间范围查询审计日志
    /// </summary>
    public async Task<PageModel<AuditLogVo>> QueryByTimeRangeAsync(DateTime startTime, DateTime endTime, int pageIndex = 1, int pageSize = 20)
    {
        var queryDto = new AuditLogQueryDto
        {
            StartTime = startTime,
            EndTime = endTime,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        return await QueryPageAsync(queryDto);
    }

    /// <summary>
    /// 获取操作类型统计
    /// </summary>
    public async Task<Dictionary<string, int>> GetOperationTypeStatisticsAsync(DateTime? startTime = null, DateTime? endTime = null)
    {
        var whereExpression = Expressionable.Create<AuditLog>();

        if (startTime.HasValue)
        {
            whereExpression.And(x => x.DateTime >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            whereExpression.And(x => x.DateTime <= endTime.Value);
        }

        var logs = await _auditLogRepository.QueryAsync(whereExpression.ToExpression());

        return logs
            .GroupBy(x => x.OperationType)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// 获取用户操作统计
    /// </summary>
    public async Task<Dictionary<string, int>> GetUserStatisticsAsync(DateTime? startTime = null, DateTime? endTime = null, int topN = 10)
    {
        var whereExpression = Expressionable.Create<AuditLog>();

        if (startTime.HasValue)
        {
            whereExpression.And(x => x.DateTime >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            whereExpression.And(x => x.DateTime <= endTime.Value);
        }

        var logs = await _auditLogRepository.QueryAsync(whereExpression.ToExpression());

        return logs
            .Where(x => !string.IsNullOrEmpty(x.UserName))
            .GroupBy(x => x.UserName!)
            .OrderByDescending(g => g.Count())
            .Take(topN)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
