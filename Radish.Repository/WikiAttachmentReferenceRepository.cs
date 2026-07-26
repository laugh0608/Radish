using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

public sealed class WikiAttachmentReferenceRepository
    : BaseRepository<WikiAttachmentReference>, IWikiAttachmentReferenceRepository
{
    public WikiAttachmentReferenceRepository(IUnitOfWorkManage unitOfWorkManage)
        : base(unitOfWorkManage)
    {
    }

    public Task<List<WikiAttachmentReference>> QueryActiveByAttachmentAsync(
        long tenantId,
        long attachmentId)
    {
        return QueryActiveByAttachmentsAsync(tenantId, [attachmentId]);
    }

    public Task<List<WikiAttachmentReference>> QueryActiveByAttachmentsAsync(
        long tenantId,
        IReadOnlyCollection<long> attachmentIds)
    {
        var normalizedIds = attachmentIds.Where(id => id > 0).Distinct().ToList();
        if (normalizedIds.Count == 0)
        {
            return Task.FromResult(new List<WikiAttachmentReference>());
        }

        return ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<WikiAttachmentReference>()
            .Where(reference =>
                reference.TenantId == tenantId &&
                normalizedIds.Contains(reference.AttachmentId) &&
                !reference.IsDeleted)
            .ToListAsync());
    }

    public Task<List<WikiAttachmentReference>> QueryActiveBySourceAsync(
        long tenantId,
        int referenceKind,
        long referenceSourceId)
    {
        return ExecuteDbOperationAsync(() => DbProtectedClient.Queryable<WikiAttachmentReference>()
            .Where(reference =>
                reference.TenantId == tenantId &&
                reference.ReferenceKind == referenceKind &&
                reference.ReferenceSourceId == referenceSourceId &&
                !reference.IsDeleted)
            .ToListAsync());
    }

    public Task SyncSourceAsync(WikiAttachmentReferenceSyncCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId < 0 || command.DocumentId <= 0 ||
            command.ReferenceKind <= 0 || command.ReferenceSourceId <= 0)
        {
            throw new ArgumentException("Wiki 附件引用来源无效。", nameof(command));
        }

        return ExecuteDbOperationAsync(async () =>
        {
            await ExecuteSourceMutationAsync(command, async () =>
            {
                var targetIds = command.AttachmentIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToHashSet();
                var existing = await DbProtectedClient.Queryable<WikiAttachmentReference>()
                    .Where(reference =>
                        reference.TenantId == command.TenantId &&
                        reference.ReferenceKind == command.ReferenceKind &&
                        reference.ReferenceSourceId == command.ReferenceSourceId)
                    .ToListAsync();

                var rowsToDeactivate = existing
                    .Where(reference => !reference.IsDeleted && !targetIds.Contains(reference.AttachmentId))
                    .Select(reference => reference.Id)
                    .ToList();
                if (rowsToDeactivate.Count > 0)
                {
                    await DbProtectedClient.Updateable<WikiAttachmentReference>()
                        .SetColumns(reference => new WikiAttachmentReference
                        {
                            IsDeleted = true,
                            DeletedAt = command.NowUtc,
                            DeletedBy = command.OperatorName,
                            ModifyTime = command.NowUtc,
                            ModifyBy = command.OperatorName,
                            ModifyId = command.OperatorId
                        })
                        .Where(reference => rowsToDeactivate.Contains(reference.Id) && !reference.IsDeleted)
                        .ExecuteCommandAsync();
                }

                foreach (var attachmentId in targetIds)
                {
                    var current = existing.FirstOrDefault(reference => reference.AttachmentId == attachmentId);
                    if (current == null)
                    {
                        await DbProtectedClient.Insertable(new WikiAttachmentReference
                        {
                            Id = SnowFlakeSingle.Instance.NextId(),
                            TenantId = command.TenantId,
                            DocumentId = command.DocumentId,
                            AttachmentId = attachmentId,
                            ReferenceKind = command.ReferenceKind,
                            ReferenceSourceId = command.ReferenceSourceId,
                            CreateTime = command.NowUtc,
                            CreateBy = command.OperatorName,
                            CreateId = command.OperatorId
                        }).ExecuteCommandAsync();
                        continue;
                    }

                    if (!current.IsDeleted && current.DocumentId == command.DocumentId)
                    {
                        continue;
                    }

                    await DbProtectedClient.Updateable<WikiAttachmentReference>()
                        .SetColumns(reference => new WikiAttachmentReference
                        {
                            DocumentId = command.DocumentId,
                            IsDeleted = false,
                            DeletedAt = null,
                            DeletedBy = null,
                            ModifyTime = command.NowUtc,
                            ModifyBy = command.OperatorName,
                            ModifyId = command.OperatorId
                        })
                        .Where(reference => reference.Id == current.Id)
                        .ExecuteCommandAsync();
                }
            });

            return true;
        });
    }

    public Task AppendRevisionAsync(WikiAttachmentReferenceSyncCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId < 0 || command.DocumentId <= 0 ||
            command.ReferenceKind != (int)Radish.Shared.CustomEnum.WikiAttachmentReferenceKind.RevisionContent ||
            command.ReferenceSourceId <= 0)
        {
            throw new ArgumentException("Wiki Revision 附件引用来源无效。", nameof(command));
        }

        return ExecuteDbOperationAsync(async () =>
        {
            await ExecuteSourceMutationAsync(command, async () =>
            {
                var targetIds = command.AttachmentIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToHashSet();
                var existing = await DbProtectedClient.Queryable<WikiAttachmentReference>()
                    .Where(reference =>
                        reference.TenantId == command.TenantId &&
                        reference.ReferenceKind == command.ReferenceKind &&
                        reference.ReferenceSourceId == command.ReferenceSourceId)
                    .ToListAsync();
                if (existing.Count > 0)
                {
                    var existingIds = existing
                        .Where(reference => !reference.IsDeleted)
                        .Select(reference => reference.AttachmentId)
                        .ToHashSet();
                    if (existing.All(reference => reference.DocumentId == command.DocumentId) &&
                        existingIds.SetEquals(targetIds))
                    {
                        return;
                    }

                    throw new WikiAttachmentReferenceConflictException();
                }

                foreach (var attachmentId in targetIds)
                {
                    await DbProtectedClient.Insertable(new WikiAttachmentReference
                    {
                        Id = SnowFlakeSingle.Instance.NextId(),
                        TenantId = command.TenantId,
                        DocumentId = command.DocumentId,
                        AttachmentId = attachmentId,
                        ReferenceKind = command.ReferenceKind,
                        ReferenceSourceId = command.ReferenceSourceId,
                        CreateTime = command.NowUtc,
                        CreateBy = command.OperatorName,
                        CreateId = command.OperatorId
                    }).ExecuteCommandAsync();
                }
            });

            return true;
        });
    }

    private async Task ExecuteSourceMutationAsync(
        WikiAttachmentReferenceSyncCommand command,
        Func<Task> mutation)
    {
        var ownsTransaction = DbProtectedClient.Ado.Transaction == null;
        if (ownsTransaction)
        {
            DbProtectedClient.Ado.BeginTran();
        }

        try
        {
            if (DbProtectedClient.CurrentConnectionConfig.DbType == DbType.PostgreSQL)
            {
                var lockKey = BuildSourceLockKey(command);
                await DbProtectedClient.Ado.ExecuteCommandAsync(
                    "SELECT pg_advisory_xact_lock(@lockKey)",
                    new SugarParameter("@lockKey", lockKey));
            }

            await mutation();
            if (ownsTransaction)
            {
                DbProtectedClient.Ado.CommitTran();
            }
        }
        catch
        {
            if (ownsTransaction)
            {
                DbProtectedClient.Ado.RollbackTran();
            }
            throw;
        }
    }

    private static long BuildSourceLockKey(WikiAttachmentReferenceSyncCommand command)
    {
        var keyBytes = Encoding.UTF8.GetBytes(
            $"wiki-attachment:{command.TenantId}:{command.ReferenceKind}:{command.ReferenceSourceId}");
        var hash = SHA256.HashData(keyBytes);
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }

    public Task<int> SoftDeleteSourceAsync(
        long tenantId,
        int referenceKind,
        long referenceSourceId,
        long operatorId,
        string operatorName,
        DateTime nowUtc)
    {
        return ExecuteDbOperationAsync(() => DbProtectedClient.Updateable<WikiAttachmentReference>()
            .SetColumns(reference => new WikiAttachmentReference
            {
                IsDeleted = true,
                DeletedAt = nowUtc,
                DeletedBy = operatorName,
                ModifyTime = nowUtc,
                ModifyBy = operatorName,
                ModifyId = operatorId
            })
            .Where(reference =>
                reference.TenantId == tenantId &&
                reference.ReferenceKind == referenceKind &&
                reference.ReferenceSourceId == referenceSourceId &&
                !reference.IsDeleted)
            .ExecuteCommandAsync());
    }

    public async Task<HashSet<long>> GetReferencedAttachmentIdsAsync(
        IReadOnlyCollection<long> attachmentIds)
    {
        var candidates = attachmentIds.Where(id => id > 0).Distinct().ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var referenced = await ExecuteDbOperationAsync(() =>
            DbProtectedClient.Queryable<WikiAttachmentReference>()
                .Where(reference =>
                    candidates.Contains(reference.AttachmentId) &&
                    !reference.IsDeleted)
                .Select(reference => reference.AttachmentId)
                .Distinct()
                .ToListAsync());
        return referenced.ToHashSet();
    }
}
