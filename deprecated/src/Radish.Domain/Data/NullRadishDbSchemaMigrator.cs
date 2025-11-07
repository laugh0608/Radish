using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Radish.Data;

/* This is used if database provider does't define
 * IRadishDbSchemaMigrator implementation.
 */
public class NullRadishDbSchemaMigrator : IRadishDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
