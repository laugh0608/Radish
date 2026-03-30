using Radish.Common.CoreTool;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>评论仓储</summary>
public class CommentRepository : BaseRepository<Comment>, ICommentRepository
{
    public CommentRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<List<Comment>> QueryLatestInteractorCommentsByPostIdsAsync(
        IReadOnlyDictionary<long, long> postAuthorMap,
        int takePerPost)
    {
        if (postAuthorMap.Count == 0 || takePerPost <= 0)
        {
            return [];
        }

        var tableName = QuoteIdentifier(DbProtectedClient.EntityMaintenance.GetEntityInfo<Comment>().DbTableName);
        var postIdColumn = QuoteIdentifier(nameof(Comment.PostId));
        var authorIdColumn = QuoteIdentifier(nameof(Comment.AuthorId));
        var createTimeColumn = QuoteIdentifier(nameof(Comment.CreateTime));
        var idColumn = QuoteIdentifier(nameof(Comment.Id));
        var isEnabledColumn = QuoteIdentifier(nameof(Comment.IsEnabled));
        var isDeletedColumn = QuoteIdentifier(nameof(Comment.IsDeleted));
        var tenantIdColumn = QuoteIdentifier(nameof(Comment.TenantId));
        const string excludedAuthorIdColumn = "\"ExcludedAuthorId\"";

        var parameters = new List<SugarParameter>
        {
            new("@takePerPost", takePerPost),
            new("@isEnabled", true),
            new("@isDeleted", false)
        };

        var requestedRows = new List<string>();
        var rowIndex = 0;
        foreach (var (postId, authorId) in postAuthorMap)
        {
            requestedRows.Add($"(@postId{rowIndex}, @excludedAuthorId{rowIndex})");
            parameters.Add(new SugarParameter($"@postId{rowIndex}", postId));
            parameters.Add(new SugarParameter($"@excludedAuthorId{rowIndex}", authorId));
            rowIndex++;
        }

        var currentTenantId = App.CurrentUser.TenantId > 0 ? App.CurrentUser.TenantId : 0;
        string tenantCondition;
        if (currentTenantId > 0)
        {
            parameters.Add(new SugarParameter("@tenantId", currentTenantId));
            tenantCondition = $"(c.{tenantIdColumn} = @tenantId OR c.{tenantIdColumn} = 0)";
        }
        else
        {
            tenantCondition = $"c.{tenantIdColumn} = 0";
        }

        var sql = $"""
WITH requested({postIdColumn}, {excludedAuthorIdColumn}) AS (
    VALUES {string.Join(", ", requestedRows)}
),
deduped AS (
    SELECT
        c.*,
        ROW_NUMBER() OVER (
            PARTITION BY c.{postIdColumn}, c.{authorIdColumn}
            ORDER BY c.{createTimeColumn} DESC, c.{idColumn} DESC
        ) AS "AuthorRank"
    FROM {tableName} AS c
    INNER JOIN requested AS r ON r.{postIdColumn} = c.{postIdColumn}
    WHERE c.{authorIdColumn} > 0
      AND c.{authorIdColumn} <> r.{excludedAuthorIdColumn}
      AND c.{isEnabledColumn} = @isEnabled
      AND c.{isDeletedColumn} = @isDeleted
      AND {tenantCondition}
),
ranked AS (
    SELECT
        deduped.*,
        ROW_NUMBER() OVER (
            PARTITION BY deduped.{postIdColumn}
            ORDER BY deduped.{createTimeColumn} DESC, deduped.{idColumn} DESC
        ) AS "PostRank"
    FROM deduped
    WHERE deduped."AuthorRank" = 1
)
SELECT *
FROM ranked
WHERE "PostRank" <= @takePerPost
ORDER BY {postIdColumn}, {createTimeColumn} DESC, {idColumn} DESC;
""";

        return await DbProtectedClient.Ado.SqlQueryAsync<Comment>(sql, parameters.ToArray());
    }

    private static string QuoteIdentifier(string identifier)
    {
        return string.Join(".", identifier
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => $"\"{part.Replace("\"", "\"\"")}\""));
    }
}
