using Xunit;

namespace Radish.Api.Tests.TestCollections;

/// <summary>隔离会初始化独立 schema 的 PostgreSQL 集成测试，避免 CodeFirst 进程级状态并发串扰。</summary>
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection
{
    public const string CollectionName = "PostgreSQL integration";
}
