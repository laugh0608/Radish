using Radish.MongoDB;
using Radish.Samples;
using Xunit;

namespace Radish.MongoDb.Applications;

[Collection(RadishTestConsts.CollectionDefinitionName)]
public class MongoDBSampleAppServiceTests : SampleAppServiceTests<RadishMongoDbTestModule>
{

}
