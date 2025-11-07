using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace ComToHtri.EntityFrameworkCore;

[ConnectionStringName(ComToHtriDbProperties.ConnectionStringName)]
public class ComToHtriDbContext : AbpDbContext<ComToHtriDbContext>, IComToHtriDbContext
{
    /* Add DbSet for each Aggregate Root here. Example:
     * public DbSet<Question> Questions { get; set; }
     */

    public ComToHtriDbContext(DbContextOptions<ComToHtriDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureComToHtri();
    }
}
