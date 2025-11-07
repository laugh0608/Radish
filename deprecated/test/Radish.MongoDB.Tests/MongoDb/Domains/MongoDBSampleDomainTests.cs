using Radish.Samples;
using Xunit;

namespace Radish.MongoDB.Domains;

[Collection(RadishTestConsts.CollectionDefinitionName)]
public class MongoDBSampleDomainTests : SampleDomainTests<RadishMongoDbTestModule>
{

}
