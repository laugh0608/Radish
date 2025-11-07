using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ComToHtri.EntityFrameworkCore;

[ConnectionStringName(ComToHtriDbProperties.ConnectionStringName)]
public interface IComToHtriDbContext : IEfCoreDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * DbSet<Question> Questions { get; }
     */
}
