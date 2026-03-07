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
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public WikiController(IWikiDocumentService wikiDocumentService, ICurrentUserAccessor currentUserAccessor)
    {
        _wikiDocumentService = wikiDocumentService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<PageModel<WikiDocumentVo>>> GetList(
        int pageIndex = 1,
        int pageSize = 20,
        string? keyword = null,
        int? status = null,
        long? parentId = null)
    {
        var result = await _wikiDocumentService.GetListAsync(
            pageIndex,
            pageSize,
            keyword,
            status,
            parentId,
            includeUnpublished: Current.IsSystemOrAdmin());

        return MessageModel<PageModel<WikiDocumentVo>>.Success("查询成功", result);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<List<WikiDocumentTreeNodeVo>>> GetTree()
    {
        var result = await _wikiDocumentService.GetTreeAsync(Current.IsSystemOrAdmin());
        return MessageModel<List<WikiDocumentTreeNodeVo>>.Success("查询成功", result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<WikiDocumentDetailVo>> GetById(long id)
    {
        var result = await _wikiDocumentService.GetDetailAsync(id, Current.IsSystemOrAdmin());
        if (result == null)
        {
            return MessageModel<WikiDocumentDetailVo>.Failed("文档不存在或无权访问", default!);
        }

        return MessageModel<WikiDocumentDetailVo>.Success("查询成功", result);
    }

    [HttpGet("{slug}")]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    public async Task<MessageModel<WikiDocumentDetailVo>> GetBySlug(string slug)
    {
        var result = await _wikiDocumentService.GetBySlugAsync(slug, Current.IsSystemOrAdmin());
        if (result == null)
        {
            return MessageModel<WikiDocumentDetailVo>.Failed("文档不存在或无权访问", default!);
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
    public async Task<MessageModel<bool>> Publish(long id)
    {
        var result = await _wikiDocumentService.PublishAsync(id, Current.UserId, Current.UserName);
        return result
            ? MessageModel<bool>.Success("发布成功", true)
            : MessageModel<bool>.Failed("文档不存在", false);
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Unpublish(long id)
    {
        var result = await _wikiDocumentService.UnpublishAsync(id, Current.UserId, Current.UserName);
        return result
            ? MessageModel<bool>.Success("已转为草稿", true)
            : MessageModel<bool>.Failed("文档不存在", false);
    }

    [HttpPost("{id:long}")]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    public async Task<MessageModel<bool>> Archive(long id)
    {
        var result = await _wikiDocumentService.ArchiveAsync(id, Current.UserId, Current.UserName);
        return result
            ? MessageModel<bool>.Success("归档成功", true)
            : MessageModel<bool>.Failed("文档不存在", false);
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
