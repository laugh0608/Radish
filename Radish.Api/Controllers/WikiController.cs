using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>Wiki 文档控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("Wiki 文档")]
public class WikiController : ControllerBase
{
    private readonly IWikiDocumentService _wikiDocumentService;
    private readonly IUserBrowseHistoryService _userBrowseHistoryService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public WikiController(
        IWikiDocumentService wikiDocumentService,
        IUserBrowseHistoryService userBrowseHistoryService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _wikiDocumentService = wikiDocumentService;
        _userBrowseHistoryService = userBrowseHistoryService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<PageModel<WikiDocumentVo>>> GetList(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        long? parentId = null,
        bool includeDeleted = false,
        bool deletedOnly = false)
    {
        var isAdmin = Current.IsSystemOrAdmin();
        var result = await _wikiDocumentService.GetListAsync(
            pageIndex,
            pageSize,
            keyword,
            status,
            parentId,
            includeUnpublished: isAdmin,
            includeDeleted: isAdmin && includeDeleted,
            deletedOnly: isAdmin && deletedOnly,
            isAuthenticated: Current.IsAuthenticated,
            roleNames: Current.Roles);

        return MessageModel<PageModel<WikiDocumentVo>>.Success("查询成功", result);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<List<WikiDocumentTreeNodeVo>>> GetTree()
    {
        var result = await _wikiDocumentService.GetTreeAsync(
            Current.IsSystemOrAdmin(),
            Current.IsAuthenticated,
            Current.Roles);
        return MessageModel<List<WikiDocumentTreeNodeVo>>.Success("查询成功", result);
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<MessageModel<WikiDocumentDetailVo>> GetById(long id, bool includeDeleted = false)
    {
        var result = await _wikiDocumentService.GetDetailAsync(
            id,
            Current.IsSystemOrAdmin(),
            Current.IsSystemOrAdmin() && includeDeleted,
            Current.IsAuthenticated,
            Current.Roles);
        if (result == null)
        {
            return MessageModel<WikiDocumentDetailVo>.Failed("文档不存在或无权访问", default!);
        }

        if (Current.UserId > 0)
        {
            await _userBrowseHistoryService.RecordAsync(new RecordBrowseHistoryDto
            {
                UserId = Current.UserId,
                TenantId = Current.TenantId,
                TargetType = "Wiki",
                TargetId = result.VoId,
                TargetSlug = result.VoSlug,
                Title = result.VoTitle,
                Summary = result.VoSummary,
                RoutePath = $"/wiki/doc/{result.VoId}",
                OperatorName = Current.UserName
            });
        }

        return MessageModel<WikiDocumentDetailVo>.Success("查询成功", result);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<MessageModel<WikiDocumentDetailVo>> GetBySlug(string slug)
    {
        var result = await _wikiDocumentService.GetBySlugAsync(
            slug,
            Current.IsSystemOrAdmin(),
            includeDeleted: false,
            isAuthenticated: Current.IsAuthenticated,
            roleNames: Current.Roles);
        if (result == null)
        {
            return MessageModel<WikiDocumentDetailVo>.Failed("文档不存在或无权访问", default!);
        }

        if (Current.UserId > 0)
        {
            await _userBrowseHistoryService.RecordAsync(new RecordBrowseHistoryDto
            {
                UserId = Current.UserId,
                TenantId = Current.TenantId,
                TargetType = "Wiki",
                TargetId = result.VoId,
                TargetSlug = result.VoSlug,
                Title = result.VoTitle,
                Summary = result.VoSummary,
                RoutePath = $"/wiki/doc/{result.VoSlug}",
                OperatorName = Current.UserName
            });
        }

        return MessageModel<WikiDocumentDetailVo>.Success("查询成功", result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<long>> Create([FromBody] CreateWikiDocumentDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return MessageModel<long>.Failed("请求参数验证失败", 0);
        }

        try
        {
            var id = await _wikiDocumentService.CreateDocumentAsync(createDto, Current.UserId, Current.UserName, Current.TenantId);
            return MessageModel<long>.Success("创建成功", id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<long>.Failed(ex.Message, 0);
        }
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Update(long id, [FromBody] UpdateWikiDocumentDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return MessageModel<bool>.Failed("请求参数验证失败", false);
        }

        try
        {
            var updated = await _wikiDocumentService.UpdateDocumentAsync(id, updateDto, Current.UserId, Current.UserName);
            return updated
                ? MessageModel<bool>.Success("更新成功", true)
                : MessageModel<bool>.Failed("文档不存在", false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<bool>.Failed(ex.Message, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Delete(long id)
    {
        try
        {
            var deleted = await _wikiDocumentService.DeleteDocumentAsync(id, Current.UserId, Current.UserName);
            return deleted
                ? MessageModel<bool>.Success("删除成功", true)
                : MessageModel<bool>.Failed("文档不存在", false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<bool>.Failed(ex.Message, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Restore(long id)
    {
        try
        {
            var restored = await _wikiDocumentService.RestoreDocumentAsync(id, Current.UserId, Current.UserName);
            return restored
                ? MessageModel<bool>.Success("恢复成功", true)
                : MessageModel<bool>.Failed("文档不存在或无法恢复", false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<bool>.Failed(ex.Message, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Publish(long id)
    {
        try
        {
            var result = await _wikiDocumentService.PublishAsync(id, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("发布成功", true)
                : MessageModel<bool>.Failed("文档不存在", false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<bool>.Failed(ex.Message, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Unpublish(long id)
    {
        try
        {
            var result = await _wikiDocumentService.UnpublishAsync(id, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("已转为草稿", true)
                : MessageModel<bool>.Failed("文档不存在", false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<bool>.Failed(ex.Message, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Archive(long id)
    {
        try
        {
            var result = await _wikiDocumentService.ArchiveAsync(id, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("归档成功", true)
                : MessageModel<bool>.Failed("文档不存在", false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<bool>.Failed(ex.Message, false);
        }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<List<WikiDocumentRevisionItemVo>>> GetRevisionList(long id)
    {
        var result = await _wikiDocumentService.GetRevisionListAsync(id);
        return MessageModel<List<WikiDocumentRevisionItemVo>>.Success("查询成功", result);
    }

    [HttpGet("{revisionId:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<WikiDocumentRevisionDetailVo>> GetRevisionDetail(long revisionId)
    {
        var result = await _wikiDocumentService.GetRevisionDetailAsync(revisionId);
        if (result == null)
        {
            return MessageModel<WikiDocumentRevisionDetailVo>.Failed("版本不存在", default!);
        }

        return MessageModel<WikiDocumentRevisionDetailVo>.Success("查询成功", result);
    }

    [HttpPost("{revisionId:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Rollback(long revisionId)
    {
        try
        {
            var result = await _wikiDocumentService.RollbackAsync(revisionId, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("回滚成功", true)
                : MessageModel<bool>.Failed("版本不存在或文档已删除", false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<bool>.Failed(ex.Message, false);
        }
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<MessageModel<long>> ImportMarkdown([FromForm] WikiMarkdownImportDto importDto)
    {
        if (!ModelState.IsValid)
        {
            return MessageModel<long>.Failed("请求参数验证失败", 0);
        }

        try
        {
            var id = await _wikiDocumentService.ImportMarkdownAsync(importDto, Current.UserId, Current.UserName, Current.TenantId);
            return MessageModel<long>.Success("导入成功", id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return MessageModel<long>.Failed(ex.Message, 0);
        }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<IActionResult> ExportMarkdown(long id)
    {
        var result = await _wikiDocumentService.ExportMarkdownAsync(id, includeUnpublished: true);
        if (result == null)
        {
            return NotFound(MessageModel<string>.Failed("文档不存在", string.Empty));
        }

        var (fileName, markdownContent) = result.Value;
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(markdownContent);
        return File(fileBytes, "text/markdown; charset=utf-8", fileName);
    }
}
