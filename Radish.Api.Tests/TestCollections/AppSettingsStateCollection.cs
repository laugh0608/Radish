using Xunit;

namespace Radish.Api.Tests.TestCollections;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class AppSettingsStateCollection
{
    public const string CollectionName = "AppSettings static state";
}
