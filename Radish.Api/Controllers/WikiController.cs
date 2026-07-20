using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Api.Routing;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>Wiki 文档控制器</summary>
[ApiController]
[ApiErrorContract]
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
    public async Task<MessageModel<PageModel<WikiDocumentVo>>> PublicGetList(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        long? parentId = null)
    {
        var result = await _wikiDocumentService.GetPublicListAsync(pageIndex, pageSize, keyword, parentId);
        return MessageModel<PageModel<WikiDocumentVo>>.Success("查询成功", result);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<List<WikiDocumentTreeNodeVo>>> PublicGetTree()
    {
        var result = await _wikiDocumentService.GetPublicTreeAsync();
        return MessageModel<List<WikiDocumentTreeNodeVo>>.Success("查询成功", result);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<MessageModel<WikiDocumentDetailVo>> PublicGetBySlug(string slug)
    {
        var result = await _wikiDocumentService.GetPublicBySlugAsync(slug);
        return result == null
            ? BuildFailure(StatusCodes.Status404NotFound, "公开文档不存在", default(WikiDocumentDetailVo)!, "Wiki.DocumentNotFound", "error.wiki.document_not_found")
            : MessageModel<WikiDocumentDetailVo>.Success("查询成功", result);
    }

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
            return BuildFailure(StatusCodes.Status404NotFound, "文档不存在或无权访问", default(WikiDocumentDetailVo)!, "Wiki.DocumentNotAccessible", "error.wiki.document_not_accessible");
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
                RoutePath = PublicRoutePathBuilder.BuildDocsPath(result.VoSlug, result.VoId),
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
            return BuildFailure(StatusCodes.Status404NotFound, "文档不存在或无权访问", default(WikiDocumentDetailVo)!, "Wiki.DocumentNotAccessible", "error.wiki.document_not_accessible");
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
                RoutePath = PublicRoutePathBuilder.BuildDocsPath(result.VoSlug, result.VoId),
                OperatorName = Current.UserName
            });
        }

        return MessageModel<WikiDocumentDetailVo>.Success("查询成功", result);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsView)]
    public async Task<MessageModel<PageModel<WikiDocumentVo>>> AdminGetList(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        int? visibility = null,
        long? parentId = null,
        string? sourceType = null,
        bool includeDeleted = false,
        bool deletedOnly = false)
    {
        var result = await _wikiDocumentService.GetGovernanceListAsync(
            pageIndex,
            pageSize,
            keyword,
            status,
            visibility,
            parentId,
            sourceType,
            includeDeleted,
            deletedOnly);
        return MessageModel<PageModel<WikiDocumentVo>>.Success("查询成功", result);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsView)]
    public async Task<MessageModel<List<WikiDocumentTreeNodeVo>>> AdminGetTree(bool includeDeleted = false)
    {
        var result = await _wikiDocumentService.GetGovernanceTreeAsync(includeDeleted);
        return MessageModel<List<WikiDocumentTreeNodeVo>>.Success("查询成功", result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsView)]
    public async Task<MessageModel<WikiDocumentDetailVo>> AdminGetById(long id, bool includeDeleted = true)
    {
        var result = await _wikiDocumentService.GetGovernanceDetailAsync(id, includeDeleted);
        if (result == null)
        {
            return BuildFailure(StatusCodes.Status404NotFound, "文档不存在", default(WikiDocumentDetailVo)!, "Wiki.DocumentNotFound", "error.wiki.document_not_found");
        }

        return MessageModel<WikiDocumentDetailVo>.Success("查询成功", result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<long>> Create([FromBody] CreateWikiDocumentDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BuildFailure(StatusCodes.Status400BadRequest, "请求参数验证失败", 0L, "Wiki.ValidationFailed", "error.wiki.validation_failed");
        }

        try
        {
            var id = await _wikiDocumentService.CreateDocumentAsync(createDto, Current.UserId, Current.UserName, Current.TenantId);
            return MessageModel<long>.Success("创建成功", id);
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure<long>(ex, 0);
        }
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Update(long id, [FromBody] UpdateWikiDocumentDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BuildFailure(StatusCodes.Status400BadRequest, "请求参数验证失败", false, "Wiki.ValidationFailed", "error.wiki.validation_failed");
        }

        try
        {
            var updated = await _wikiDocumentService.UpdateDocumentAsync(id, updateDto, Current.UserId, Current.UserName);
            return updated
                ? MessageModel<bool>.Success("更新成功", true)
                : BuildFailure(StatusCodes.Status404NotFound, "文档不存在", false, "Wiki.DocumentNotFound", "error.wiki.document_not_found");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsDelete)]
    public async Task<MessageModel<bool>> Delete(long id)
    {
        try
        {
            var deleted = await _wikiDocumentService.DeleteDocumentAsync(id, Current.UserId, Current.UserName);
            return deleted
                ? MessageModel<bool>.Success("删除成功", true)
                : BuildFailure(StatusCodes.Status404NotFound, "文档不存在", false, "Wiki.DocumentNotFound", "error.wiki.document_not_found");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsRestore)]
    public async Task<MessageModel<bool>> Restore(long id)
    {
        try
        {
            var restored = await _wikiDocumentService.RestoreDocumentAsync(id, Current.UserId, Current.UserName);
            return restored
                ? MessageModel<bool>.Success("恢复成功", true)
                : BuildFailure(StatusCodes.Status409Conflict, "文档不存在或无法恢复", false, "Wiki.RestoreRejected", "error.wiki.restore_rejected");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsPublish)]
    public async Task<MessageModel<bool>> Publish(long id)
    {
        try
        {
            var result = await _wikiDocumentService.PublishAsync(id, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("发布成功", true)
                : BuildFailure(StatusCodes.Status404NotFound, "文档不存在", false, "Wiki.DocumentNotFound", "error.wiki.document_not_found");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsPublish)]
    public async Task<MessageModel<bool>> Unpublish(long id)
    {
        try
        {
            var result = await _wikiDocumentService.UnpublishAsync(id, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("已转为草稿", true)
                : BuildFailure(StatusCodes.Status404NotFound, "文档不存在", false, "Wiki.DocumentNotFound", "error.wiki.document_not_found");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsArchive)]
    public async Task<MessageModel<bool>> Archive(long id)
    {
        try
        {
            var result = await _wikiDocumentService.ArchiveAsync(id, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("归档成功", true)
                : BuildFailure(StatusCodes.Status404NotFound, "文档不存在", false, "Wiki.DocumentNotFound", "error.wiki.document_not_found");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsPermissions)]
    public async Task<MessageModel<bool>> UpdateAccessPolicy(long id, [FromBody] UpdateWikiDocumentAccessPolicyDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BuildFailure(StatusCodes.Status400BadRequest, "请求参数验证失败", false, "Wiki.ValidationFailed", "error.wiki.validation_failed");
        }

        try
        {
            var updated = await _wikiDocumentService.UpdateAccessPolicyAsync(id, updateDto, Current.UserId, Current.UserName);
            return updated
                ? MessageModel<bool>.Success("访问策略已更新", true)
                : BuildFailure(StatusCodes.Status404NotFound, "文档不存在", false, "Wiki.DocumentNotFound", "error.wiki.document_not_found");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsView)]
    public async Task<MessageModel<List<WikiDocumentRevisionItemVo>>> GetRevisionList(long id)
    {
        var result = await _wikiDocumentService.GetRevisionListAsync(id);
        return MessageModel<List<WikiDocumentRevisionItemVo>>.Success("查询成功", result);
    }

    [HttpGet("{revisionId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsView)]
    public async Task<MessageModel<WikiDocumentRevisionDetailVo>> GetRevisionDetail(long revisionId)
    {
        var result = await _wikiDocumentService.GetRevisionDetailAsync(revisionId);
        if (result == null)
        {
            return BuildFailure(StatusCodes.Status404NotFound, "版本不存在", default(WikiDocumentRevisionDetailVo)!, "Wiki.RevisionNotFound", "error.wiki.revision_not_found");
        }

        return MessageModel<WikiDocumentRevisionDetailVo>.Success("查询成功", result);
    }

    [HttpPost("{revisionId:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsRollback)]
    public async Task<MessageModel<bool>> Rollback(long revisionId)
    {
        try
        {
            var result = await _wikiDocumentService.RollbackAsync(revisionId, Current.UserId, Current.UserName);
            return result
                ? MessageModel<bool>.Success("回滚成功", true)
                : BuildFailure(StatusCodes.Status409Conflict, "版本不存在或文档已删除", false, "Wiki.RollbackRejected", "error.wiki.rollback_rejected");
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure(ex, false);
        }
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsImport)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<MessageModel<long>> ImportMarkdown([FromForm] WikiMarkdownImportDto importDto)
    {
        if (!ModelState.IsValid)
        {
            return BuildFailure(StatusCodes.Status400BadRequest, "请求参数验证失败", 0L, "Wiki.ValidationFailed", "error.wiki.validation_failed");
        }

        try
        {
            var id = await _wikiDocumentService.ImportMarkdownAsync(importDto, Current.UserId, Current.UserName, Current.TenantId);
            return MessageModel<long>.Success("导入成功", id);
        }
        catch (Exception ex) when (ex is ArgumentException or BusinessException)
        {
            return BuildFailure<long>(ex, 0);
        }
    }

    private static MessageModel<T> BuildFailure<T>(Exception exception, T responseData)
    {
        var businessException = exception as BusinessException;
        return BuildFailure(
            businessException?.StatusCode ?? StatusCodes.Status400BadRequest,
            exception.Message,
            responseData,
            businessException?.ErrorCode ?? "Wiki.ValidationFailed",
            businessException?.MessageKey ?? "error.wiki.validation_failed");
    }

    private static MessageModel<T> BuildFailure<T>(
        int statusCode,
        string message,
        T responseData,
        string code,
        string messageKey)
    {
        return new MessageModel<T>
        {
            IsSuccess = false,
            StatusCode = statusCode,
            MessageInfo = message,
            Code = code,
            MessageKey = messageKey,
            ResponseData = responseData
        };
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [RequireConsolePermission(ConsolePermissions.DocsExport)]
    public async Task<IActionResult> ExportMarkdown(long id)
    {
        var result = await _wikiDocumentService.ExportMarkdownAsync(id, includeUnpublished: true);
        if (result == null)
        {
            return NotFound(BuildFailure(StatusCodes.Status404NotFound, "文档不存在", string.Empty, "Wiki.DocumentNotFound", "error.wiki.document_not_found"));
        }

        var (fileName, markdownContent) = result.Value;
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(markdownContent);
        return File(fileBytes, "text/markdown; charset=utf-8", fileName);
    }
}
