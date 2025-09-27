using ComToHtri.MongoDB;
using ComToHtri.Samples;
using Xunit;

namespace ComToHtri.MongoDb.Applications;

[Collection(MongoTestCollection.Name)]
public class MongoDBSampleAppService_Tests : SampleAppService_Tests<ComToHtriMongoDbTestModule>
{

}
