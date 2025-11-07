using System.Threading.Tasks;

namespace Radish.Data;

public interface IRadishDbSchemaMigrator
{
    Task MigrateAsync();
}
