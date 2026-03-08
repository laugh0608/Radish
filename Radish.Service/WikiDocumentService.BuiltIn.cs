using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Radish.Common.CoreTool;
using Radish.Common.DocumentTool;
using Radish.Model;
using Radish.Shared.CustomEnum;

namespace Radish.Service;

public partial class WikiDocumentService
{
    private sealed class BuiltInDocumentDescriptor
    {
        public required string PathKey { get; init; }
        public string? RelativePath { get; init; }
        public required string Slug { get; init; }
        public required string Title { get; set; }
        public string? Summary { get; set; }
        public required string MarkdownContent { get; set; }
        public required string SourcePath { get; init; }
        public bool IsGenerated { get; init; }
        public bool IsContainer { get; init; }
        public string? ParentPathKey { get; set; }
        public int Sort { get; set; }
        public int Depth => string.IsNullOrWhiteSpace(PathKey) ? 0 : PathKey.Count(c => c == '/') + 1;
    }

    private sealed class BuiltInUpsertResult
    {
        public WikiDocument? Document { get; init; }
        public bool Created { get; init; }
        public bool Updated { get; init; }
        public bool Restored { get; init; }
        public bool Skipped { get; init; }
    }

    internal const string BuiltInSourceType = "BuiltIn";
    private static readonly Regex MarkdownLinkRegex = new(@"(?<prefix>!?\[[^\]]*\]\()(?<url>[^)]+)(?<suffix>\))", RegexOptions.Compiled);
    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown" };
    public async Task<WikiBuiltInSyncSummary> SyncBuiltInDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var summary = new WikiBuiltInSyncSummary();

        if (!ShouldIncludeBuiltInDocuments())
        {
            summary.IsSkipped = true;
            summary.SkipReason = "Document.ShowBuiltInDocs=false";
            return summary;
        }

        var docsRoot = ResolveBuiltInDocsRoot();
        if (!Directory.Exists(docsRoot))
        {
            summary.IsSkipped = true;
            summary.SkipReason = $"固定文档目录不存在: {docsRoot}";
            return summary;
        }

        var descriptors = await BuildBuiltInDocumentDescriptorsAsync(docsRoot, cancellationToken);
        summary.MarkdownFileCount = descriptors.Count(d => !d.IsGenerated);
        summary.DescriptorCount = descriptors.Count;
        summary.GeneratedNodeCount = descriptors.Count(d => d.IsGenerated);
        if (descriptors.Count == 0)
        {
            summary.IsSkipped = true;
            summary.SkipReason = "未扫描到可同步的固定 Markdown 文档";
            return summary;
        }

        var activeSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathDocumentMap = new Dictionary<string, WikiDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors.OrderBy(d => d.Depth).ThenBy(d => d.Sort).ThenBy(d => d.PathKey, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var upsertResult = await UpsertBuiltInDocumentAsync(descriptor);
            if (upsertResult.Skipped || upsertResult.Document == null)
            {
                summary.SkippedCount++;
                continue;
            }

            if (upsertResult.Created)
            {
                summary.CreatedCount++;
            }

            if (upsertResult.Updated)
            {
                summary.UpdatedCount++;
            }

            if (upsertResult.Restored)
            {
                summary.RestoredCount++;
            }

            activeSlugs.Add(descriptor.Slug);
            pathDocumentMap[descriptor.PathKey] = upsertResult.Document;
        }

        foreach (var descriptor in descriptors.OrderBy(d => d.Depth).ThenBy(d => d.Sort).ThenBy(d => d.PathKey, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!pathDocumentMap.TryGetValue(descriptor.PathKey, out var document))
            {
                continue;
            }

            var parentId = ResolveBuiltInParentId(descriptor.ParentPathKey, pathDocumentMap);
            if (document.ParentId != parentId || document.Sort != descriptor.Sort)
            {
                document.ParentId = parentId;
                document.Sort = descriptor.Sort;
                document.ModifyId = 0;
                document.ModifyBy = "System";
                document.ModifyTime = DateTime.Now;
                await UpdateAsync(document);
                summary.ParentAdjustedCount++;
            }
        }

        var builtInDocuments = await _wikiDocumentRepository.QueryAsync(d => d.SourceType == BuiltInSourceType);
        foreach (var document in builtInDocuments.Where(d => !activeSlugs.Contains(d.Slug)))
        {
            var deleted = await _wikiDocumentRepository.SoftDeleteByIdAsync(document.Id, "System");
            if (deleted)
            {
                summary.SoftDeletedCount++;
            }
        }

        summary.SyncedCount = pathDocumentMap.Count;
        return summary;
    }

    private bool ShouldIncludeBuiltInDocuments() => _documentOptions.ShowBuiltInDocs;

    private bool ShouldExposeDocument(WikiDocument document, bool includeUnpublished, bool includeDeleted = false)
    {
        if (document.IsDeleted && !includeDeleted)
        {
            return false;
        }

        if (!ShouldIncludeBuiltInDocuments() && IsBuiltInSourceType(document.SourceType))
        {
            return false;
        }

        return includeUnpublished || document.Status == (int)WikiDocumentStatusEnum.Published;
    }

    private static bool IsBuiltInSourceType(string? sourceType)
    {
        return string.Equals(sourceType, BuiltInSourceType, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureDocumentIsEditable(WikiDocument document)
    {
        if (IsBuiltInSourceType(document.SourceType))
        {
            throw new InvalidOperationException("固定文档为只读内容，请修改 Docs 目录中的源文件");
        }
    }

    private string ResolveBuiltInDocsRoot()
    {
        var solutionRoot = AppPathTool.GetSolutionRootOrBasePath();
        var configuredPath = _documentOptions.BuiltInDocsPath?.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "Docs";
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(solutionRoot, configuredPath));
    }

    private static bool ShouldSyncBuiltInMarkdownFile(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        return !normalized.EndsWith("/README.md", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<BuiltInDocumentDescriptor>> BuildBuiltInDocumentDescriptorsAsync(string docsRoot, CancellationToken cancellationToken)
    {
        var markdownFiles = Directory.GetFiles(docsRoot, "*.md", SearchOption.AllDirectories)
            .Where(ShouldSyncBuiltInMarkdownFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var descriptors = new Dictionary<string, BuiltInDocumentDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in markdownFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(docsRoot, filePath).Replace('\\', '/');
            var pathKey = BuildBuiltInPathKey(relativePath);
            var markdown = await File.ReadAllTextAsync(filePath, cancellationToken);

            descriptors[pathKey] = new BuiltInDocumentDescriptor
            {
                PathKey = pathKey,
                RelativePath = relativePath,
                Slug = BuildBuiltInSlug(pathKey),
                Title = ExtractBuiltInTitle(markdown, relativePath),
                Summary = ExtractBuiltInSummary(markdown),
                MarkdownContent = RewriteBuiltInMarkdown(markdown, relativePath, docsRoot),
                SourcePath = $"Docs/{relativePath}",
                IsGenerated = false,
                IsContainer = IsDirectoryIndexMarkdown(relativePath)
            };
        }

        var directoryKeys = descriptors.Keys
            .SelectMany(EnumerateAncestorPathKeys)
            .Where(pathKey => !string.IsNullOrWhiteSpace(pathKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pathKey => pathKey.Count(c => c == '/'))
            .ThenBy(pathKey => pathKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var directoryKey in directoryKeys)
        {
            if (descriptors.ContainsKey(directoryKey))
            {
                continue;
            }

            var title = BuildGeneratedDirectoryTitle(directoryKey);
            descriptors[directoryKey] = new BuiltInDocumentDescriptor
            {
                PathKey = directoryKey,
                RelativePath = null,
                Slug = BuildBuiltInSlug(directoryKey),
                Title = title,
                Summary = $"{title}目录下的固定文档导航",
                MarkdownContent = string.Empty,
                SourcePath = $"Docs/{directoryKey}/",
                IsGenerated = true,
                IsContainer = true
            };
        }

        var pathKeys = descriptors.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors.Values)
        {
            descriptor.ParentPathKey = ResolveBuiltInParentPathKey(descriptor.PathKey, pathKeys);
        }

        foreach (var descriptor in descriptors.Values.Where(d => d.IsGenerated))
        {
            var childDescriptors = descriptors.Values
                .Where(child => string.Equals(child.ParentPathKey, descriptor.PathKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(child => child.IsContainer)
                .ThenBy(child => child.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            descriptor.MarkdownContent = BuildGeneratedDirectoryMarkdown(descriptor, childDescriptors);
        }

        AssignBuiltInSorts(descriptors.Values);
        return descriptors.Values.ToList();
    }

    private async Task<BuiltInUpsertResult> UpsertBuiltInDocumentAsync(BuiltInDocumentDescriptor descriptor)
    {
        var restoredCount = await _wikiDocumentRepository.RestoreAsync(d => d.SourceType == BuiltInSourceType && d.Slug == descriptor.Slug);
        var existing = await _wikiDocumentRepository.QueryFirstAsync(d => d.Slug == descriptor.Slug);

        if (existing == null)
        {
            var document = new WikiDocument
            {
                Title = descriptor.Title,
                Slug = descriptor.Slug,
                Summary = descriptor.Summary,
                MarkdownContent = descriptor.MarkdownContent,
                ParentId = null,
                Sort = descriptor.Sort,
                Status = (int)WikiDocumentStatusEnum.Published,
                SourceType = BuiltInSourceType,
                SourcePath = descriptor.SourcePath,
                Version = 1,
                TenantId = 0,
                PublishedAt = DateTime.Now,
                CreateId = 0,
                CreateBy = "System",
                CreateTime = DateTime.Now,
                ModifyId = 0,
                ModifyBy = "System",
                ModifyTime = DateTime.Now
            };

            var id = await AddAsync(document);
            document.Id = id;
            await EnsureBuiltInRevisionAsync(document);
            return new BuiltInUpsertResult
            {
                Document = document,
                Created = true,
                Restored = restoredCount > 0
            };
        }

        if (!IsBuiltInSourceType(existing.SourceType))
        {
            return new BuiltInUpsertResult
            {
                Document = null,
                Skipped = true,
                Restored = restoredCount > 0
            };
        }

        existing.Title = descriptor.Title;
        existing.Summary = descriptor.Summary;
        existing.MarkdownContent = descriptor.MarkdownContent;
        existing.Status = (int)WikiDocumentStatusEnum.Published;
        existing.SourcePath = descriptor.SourcePath;
        existing.PublishedAt ??= DateTime.Now;
        existing.ModifyId = 0;
        existing.ModifyBy = "System";
        existing.ModifyTime = DateTime.Now;
        existing.IsDeleted = false;
        existing.DeletedAt = null;
        existing.DeletedBy = null;
        existing.Version = 1;
        await UpdateAsync(existing);
        await EnsureBuiltInRevisionAsync(existing);
        return new BuiltInUpsertResult
        {
            Document = existing,
            Updated = true,
            Restored = restoredCount > 0
        };
    }

    private async Task EnsureBuiltInRevisionAsync(WikiDocument document)
    {
        var revision = await _wikiDocumentRevisionRepository.QueryFirstAsync(r => r.DocumentId == document.Id && r.Version == 1);
        if (revision == null)
        {
            await _wikiDocumentRevisionRepository.AddAsync(new WikiDocumentRevision
            {
                DocumentId = document.Id,
                Version = 1,
                Title = document.Title,
                MarkdownContent = document.MarkdownContent,
                ChangeSummary = "固定文档同步",
                SourceType = BuiltInSourceType,
                TenantId = document.TenantId,
                CreateId = 0,
                CreateBy = "System",
                CreateTime = DateTime.Now
            });
            return;
        }

        revision.Title = document.Title;
        revision.MarkdownContent = document.MarkdownContent;
        revision.ChangeSummary = "固定文档同步";
        revision.SourceType = BuiltInSourceType;
        await _wikiDocumentRevisionRepository.UpdateAsync(revision);
    }

    private string RewriteBuiltInMarkdown(string markdown, string relativePath, string docsRoot)
    {
        return MarkdownLinkRegex.Replace(markdown, match =>
        {
            var rawUrl = match.Groups["url"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawUrl) ||
                rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith('#'))
            {
                return match.Value;
            }

            var rewritten = RewriteBuiltInUrl(rawUrl, relativePath, docsRoot);
            return $"{match.Groups["prefix"].Value}{rewritten}{match.Groups["suffix"].Value}";
        });
    }

    private string RewriteBuiltInUrl(string rawUrl, string currentRelativePath, string docsRoot)
    {
        var workingUrl = rawUrl.Trim();
        var fragment = string.Empty;
        var query = string.Empty;

        var fragmentIndex = workingUrl.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            fragment = workingUrl[fragmentIndex..];
            workingUrl = workingUrl[..fragmentIndex];
        }

        var queryIndex = workingUrl.IndexOf('?');
        if (queryIndex >= 0)
        {
            query = workingUrl[queryIndex..];
            workingUrl = workingUrl[..queryIndex];
        }

        var cleanUrl = workingUrl.Trim();
        if (string.IsNullOrWhiteSpace(cleanUrl))
        {
            return rawUrl;
        }

        var resolvedPath = ResolveLinkedPath(cleanUrl, currentRelativePath, docsRoot);
        var extension = Path.GetExtension(resolvedPath);

        if (MarkdownExtensions.Contains(extension) || string.IsNullOrWhiteSpace(extension))
        {
            var slug = BuildBuiltInSlug(BuildBuiltInPathKey(resolvedPath));
            return $"/__documents__/{Uri.EscapeDataString(slug)}{query}{fragment}";
        }

        if (BuiltInDocumentStaticAssetPolicy.IsAllowedAssetPath(resolvedPath))
        {
            var requestPath = (_documentOptions.StaticAssetsRequestPath?.TrimEnd('/') ?? "/docs-assets").TrimEnd('/');
            return $"{requestPath}/{resolvedPath.Replace('\\', '/').TrimStart('/')}{query}{fragment}";
        }

        return rawUrl;
    }

    private static string ResolveLinkedPath(string rawPath, string currentRelativePath, string docsRoot)
    {
        var currentDirectory = Path.GetDirectoryName(currentRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var candidatePath = rawPath.StartsWith('/')
            ? rawPath.TrimStart('/')
            : Path.Combine(currentDirectory, rawPath).Replace('\\', '/');

        return TryResolveLinkedFile(candidatePath, docsRoot) ?? NormalizeRelativePath(candidatePath, docsRoot);
    }

    private static string? TryResolveLinkedFile(string candidatePath, string docsRoot)
    {
        var normalizedCandidate = NormalizeRelativePath(candidatePath, docsRoot);
        var extension = Path.GetExtension(normalizedCandidate);
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(extension))
        {
            candidates.Add(normalizedCandidate);
        }
        else
        {
            candidates.Add(normalizedCandidate);
            candidates.Add($"{normalizedCandidate}.md");
            candidates.Add($"{normalizedCandidate}.markdown");
            candidates.Add($"{normalizedCandidate}/index.md");
            candidates.Add($"{normalizedCandidate}/index.markdown");
        }

        foreach (var candidate in candidates)
        {
            var fullPath = GetSafePathUnderDocsRoot(candidate, docsRoot);
            if (fullPath != null && File.Exists(fullPath))
            {
                return Path.GetRelativePath(docsRoot, fullPath).Replace('\\', '/');
            }
        }

        return null;
    }

    private static string NormalizeRelativePath(string relativePath, string docsRoot)
    {
        var fullPath = GetSafePathUnderDocsRoot(relativePath, docsRoot);
        if (fullPath == null)
        {
            return relativePath.Replace('\\', '/').TrimStart('/');
        }

        return Path.GetRelativePath(docsRoot, fullPath).Replace('\\', '/');
    }

    private static string? GetSafePathUnderDocsRoot(string relativePath, string docsRoot)
    {
        var docsRootFullPath = Path.GetFullPath(docsRoot);
        var fullPath = Path.GetFullPath(Path.Combine(docsRootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var docsRootPrefix = docsRootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (fullPath.Equals(docsRootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath.StartsWith(docsRootPrefix, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }

    private static string BuildBuiltInPathKey(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/').Trim('/');
        if (string.Equals(normalizedPath, "index.md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedPath, "index.markdown", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (normalizedPath.EndsWith("/index.md", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath[..^"/index.md".Length];
        }

        if (normalizedPath.EndsWith("/index.markdown", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath[..^"/index.markdown".Length];
        }

        var extension = Path.GetExtension(normalizedPath);
        return string.IsNullOrWhiteSpace(extension)
            ? normalizedPath
            : normalizedPath[..^extension.Length];
    }

    private static string BuildBuiltInSlug(string pathKey)
    {
        if (string.IsNullOrWhiteSpace(pathKey))
        {
            return "index";
        }

        return BuildSlug(pathKey.Replace('/', '-'));
    }

    private static bool IsDirectoryIndexMarkdown(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/').Trim('/');
        return string.Equals(normalizedPath, "index.md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedPath, "index.markdown", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.EndsWith("/index.md", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.EndsWith("/index.markdown", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateAncestorPathKeys(string pathKey)
    {
        if (string.IsNullOrWhiteSpace(pathKey))
        {
            yield break;
        }

        var segments = pathKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var length = 1; length <= segments.Length; length++)
        {
            yield return string.Join('/', segments.Take(length));
        }
    }

    private static string? ResolveBuiltInParentPathKey(string pathKey, ISet<string> existingPathKeys)
    {
        if (string.IsNullOrWhiteSpace(pathKey))
        {
            return null;
        }

        var current = pathKey;
        while (true)
        {
            var lastSlashIndex = current.LastIndexOf('/');
            if (lastSlashIndex < 0)
            {
                break;
            }

            current = current[..lastSlashIndex];
            if (existingPathKeys.Contains(current))
            {
                return current;
            }
        }

        return existingPathKeys.Contains(string.Empty) ? string.Empty : null;
    }

    private static long? ResolveBuiltInParentId(string? parentPathKey, IReadOnlyDictionary<string, WikiDocument> pathDocumentMap)
    {
        if (string.IsNullOrWhiteSpace(parentPathKey))
        {
            return parentPathKey == string.Empty && pathDocumentMap.TryGetValue(string.Empty, out var rootDocument)
                ? rootDocument.Id
                : null;
        }

        return pathDocumentMap.TryGetValue(parentPathKey, out var document) ? document.Id : null;
    }

    private static void AssignBuiltInSorts(IEnumerable<BuiltInDocumentDescriptor> descriptors)
    {
        var groups = descriptors
            .GroupBy(descriptor => descriptor.ParentPathKey, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var orderedDescriptors = group
                .OrderByDescending(descriptor => descriptor.IsContainer)
                .ThenBy(descriptor => descriptor.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(descriptor => descriptor.PathKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var index = 0; index < orderedDescriptors.Count; index++)
            {
                orderedDescriptors[index].Sort = (index + 1) * 10;
            }
        }
    }

    private static string BuildGeneratedDirectoryTitle(string pathKey)
    {
        if (string.IsNullOrWhiteSpace(pathKey))
        {
            return "文档";
        }

        var normalizedPath = pathKey.Replace('\\', '/').Trim('/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var lastSegment = segments[^1];

        return lastSegment.ToLowerInvariant() switch
        {
            "architecture" => "架构设计",
            "guide" => "开发指南",
            "frontend" => "前端开发",
            "features" => "功能设计",
            "deployment" => "部署运维",
            "changelog" => "更新日志",
            _ => ConvertDirectorySegmentToTitle(lastSegment)
        };
    }

    private static string ConvertDirectorySegmentToTitle(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "未命名目录";
        }

        if (segment.Any(ch => ch > 127))
        {
            return segment;
        }

        var normalized = segment.Replace('_', '-').Trim('-');
        var textInfo = CultureInfo.InvariantCulture.TextInfo;
        return textInfo.ToTitleCase(normalized);
    }

    private static string BuildGeneratedDirectoryMarkdown(BuiltInDocumentDescriptor descriptor, List<BuiltInDocumentDescriptor> childDescriptors)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {descriptor.Title}");
        builder.AppendLine();
        builder.AppendLine("> 本页由 `Docs/` 目录自动生成，用于聚合该分类下的固定文档。");

        if (childDescriptors.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("当前分类下暂时还没有可展示的固定文档。");
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine();
        builder.AppendLine("## 子文档");
        builder.AppendLine();

        foreach (var child in childDescriptors)
        {
            builder.Append("- [");
            builder.Append(child.Title);
            builder.Append("](/__documents__/");
            builder.Append(Uri.EscapeDataString(child.Slug));
            builder.Append(')');

            if (!string.IsNullOrWhiteSpace(child.Summary))
            {
                builder.Append(" - ");
                builder.Append(child.Summary);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string ExtractBuiltInTitle(string markdownContent, string relativePath)
    {
        var title = ExtractTitle(markdownContent);
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        if (IsDirectoryIndexMarkdown(relativePath))
        {
            var pathKey = BuildBuiltInPathKey(relativePath);
            return string.IsNullOrWhiteSpace(pathKey)
                ? "文档"
                : BuildGeneratedDirectoryTitle(pathKey);
        }

        return Path.GetFileNameWithoutExtension(relativePath);
    }

    private static string? ExtractBuiltInSummary(string markdownContent)
    {
        using var reader = new StringReader(markdownContent);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('>') || trimmed.StartsWith('-') || trimmed.StartsWith('*') || trimmed.StartsWith("```"))
            {
                continue;
            }

            return trimmed.Length > 180 ? trimmed[..180] : trimmed;
        }

        return null;
    }
}
