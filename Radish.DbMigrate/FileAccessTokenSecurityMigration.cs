using Radish.Common.Security;
using Radish.Model.Models;
using SqlSugar;

namespace Radish.DbMigrate;

public static class FileAccessTokenSecurityMigration
{
    public static int Apply(ISqlSugarClient db)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<FileAccessToken>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return 0;
        }

        var tokens = db.Queryable<FileAccessToken>().ToList();
        var invalidTokens = tokens
            .Where(token =>
                !FileAccessTokenHashing.IsLegacyRawToken(token.TokenHash) &&
                !FileAccessTokenHashing.IsStoredHash(token.TokenHash))
            .Select(token => token.Id)
            .ToList();
        if (invalidTokens.Count > 0)
        {
            throw new InvalidOperationException(
                $"FileAccessToken 存在 {invalidTokens.Count} 条无法识别的 token 格式，记录 ID: {string.Join(", ", invalidTokens.Take(10))}");
        }

        var legacyTokens = tokens
            .Where(token => FileAccessTokenHashing.IsLegacyRawToken(token.TokenHash))
            .ToList();
        if (legacyTokens.Count == 0)
        {
            return 0;
        }

        db.Ado.BeginTran();
        try
        {
            foreach (var token in legacyTokens)
            {
                var tokenHash = FileAccessTokenHashing.HashToken(token.TokenHash);
                var updated = db.Updateable<FileAccessToken>()
                    .SetColumns(entity => new FileAccessToken { TokenHash = tokenHash })
                    .Where(entity => entity.Id == token.Id && entity.TokenHash == token.TokenHash)
                    .ExecuteCommand();
                if (updated != 1)
                {
                    throw new InvalidOperationException($"FileAccessToken 记录 {token.Id} 原位 hash 失败。");
                }
            }

            db.Ado.CommitTran();
            return legacyTokens.Count;
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    public static IReadOnlyList<string> Verify(ISqlSugarClient db)
    {
        var tableName = db.EntityMaintenance.GetEntityInfo<FileAccessToken>().DbTableName;
        if (!db.DbMaintenance.IsAnyTable(tableName, false))
        {
            return [];
        }

        var tokens = db.Queryable<FileAccessToken>()
            .Select(token => new { token.Id, token.TokenHash })
            .ToList();
        var issues = new List<string>();

        foreach (var token in tokens)
        {
            if (!FileAccessTokenHashing.IsStoredHash(token.TokenHash))
            {
                issues.Add($"FileAccessToken 记录 {token.Id} 仍包含非 hash token。");
            }
        }

        var duplicateHashes = tokens
            .GroupBy(token => token.TokenHash, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateHashes.Count > 0)
        {
            issues.Add($"FileAccessToken 存在 {duplicateHashes.Count} 组重复 hash。");
        }

        return issues;
    }
}
