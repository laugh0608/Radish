using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Radish.Auth.OpenIddict;

internal static class AuthOpenIddictModelConsistency
{
    public static IReadOnlyList<string> Inspect(AuthOpenIddictDbContext db)
    {
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var snapshot = migrationsAssembly.ModelSnapshot;
        if (snapshot is null)
        {
            return ["MissingModelSnapshot"];
        }

        var modelRuntimeInitializer = db.GetService<IModelRuntimeInitializer>();
        var snapshotModel = modelRuntimeInitializer.Initialize(snapshot.Model);
        var designTimeModel = db.GetService<IDesignTimeModel>().Model;
        var modelDiffer = db.GetService<IMigrationsModelDiffer>();

        return modelDiffer
            .GetDifferences(snapshotModel.GetRelationalModel(), designTimeModel.GetRelationalModel())
            .Select(Describe)
            .ToList();
    }

    private static string Describe(MigrationOperation operation)
    {
        return operation switch
        {
            AlterColumnOperation column =>
                $"AlterColumn:{column.Table}.{column.Name}:" +
                $"{DescribeColumn(column.OldColumn)}->{DescribeColumn(column)}",
            AddColumnOperation column =>
                $"AddColumn:{column.Table}.{column.Name}:{DescribeColumn(column)}",
            DropColumnOperation column => $"DropColumn:{column.Table}.{column.Name}",
            CreateTableOperation table => $"CreateTable:{table.Name}",
            DropTableOperation table => $"DropTable:{table.Name}",
            CreateIndexOperation index => $"CreateIndex:{index.Table}.{index.Name}",
            DropIndexOperation index => $"DropIndex:{index.Table}.{index.Name}",
            AddForeignKeyOperation foreignKey => $"AddForeignKey:{foreignKey.Table}.{foreignKey.Name}",
            DropForeignKeyOperation foreignKey => $"DropForeignKey:{foreignKey.Table}.{foreignKey.Name}",
            AlterDatabaseOperation database =>
                $"AlterDatabase:{DescribeAnnotations(database.GetAnnotations())}",
            _ => $"{operation.GetType().Name}:{DescribeAnnotations(operation.GetAnnotations())}"
        };
    }

    private static string DescribeColumn(ColumnOperation column)
    {
        return $"ClrType={column.ClrType.Name},ColumnType={column.ColumnType ?? "<null>"}," +
               $"Nullable={column.IsNullable},MaxLength={column.MaxLength?.ToString() ?? "<null>"}," +
               $"Annotations={DescribeAnnotations(column.GetAnnotations())}";
    }

    private static string DescribeAnnotations(IEnumerable<IAnnotation> annotations)
    {
        var values = annotations
            .OrderBy(annotation => annotation.Name, StringComparer.Ordinal)
            .Select(annotation => $"{annotation.Name}={annotation.Value ?? "<null>"}")
            .ToList();
        return values.Count == 0 ? "<none>" : string.Join(",", values);
    }
}
