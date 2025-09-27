using ComToHtri.Samples;
using Xunit;

namespace ComToHtri.MongoDB.Domains;

[Collection(MongoTestCollection.Name)]
public class MongoDBSampleDomain_Tests : SampleManager_Tests<ComToHtriMongoDbTestModule>
{

}
