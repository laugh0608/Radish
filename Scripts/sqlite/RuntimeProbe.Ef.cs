using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Radish.SqliteDiagnostics;

public static partial class SqliteNativeRuntimeProbe
{
    private static void CheckEf()
    {
        using var db = new DbContext(new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:;Pooling=False").Options);
        db.Database.OpenConnection();
        db.Database.ExecuteSqlRaw("CREATE TABLE ef_probe(id INTEGER PRIMARY KEY, value TEXT NOT NULL)");
        using (var transaction = db.Database.BeginTransaction())
        {
            db.Database.ExecuteSqlRaw("INSERT INTO ef_probe VALUES (1, 'rollback')");
            transaction.Rollback();
        }
        Require(db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM ef_probe").Single() == 0, "EF transaction did not roll back");
        Require(db.Database.SqlQueryRaw<string>("SELECT sqlite_version() AS Value").Single() == Version, "EF SQLite version mismatch");
    }

}
