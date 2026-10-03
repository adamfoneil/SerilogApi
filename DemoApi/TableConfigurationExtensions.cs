using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SerilogQueryApi;

namespace DemoApi;

/// <summary>
/// Extension methods for configuring EF Core entities using TableConfiguration.
/// </summary>
public static class TableConfigurationExtensions
{
    /// <summary>
    /// Configures an entity builder using the provided TableConfiguration.
    /// Automatically maps columns, sets max lengths, column types, and precisions.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being configured</typeparam>
    /// <param name="entity">The entity builder</param>
    /// <param name="tableConfig">The table configuration</param>
    public static void ConfigureFromTableConfiguration<TEntity>(
        this EntityTypeBuilder<TEntity> entity,
        TableConfiguration tableConfig)
        where TEntity : class
    {
        entity.ToTable(tableConfig.TableName);

        var entityType = typeof(TEntity);

        foreach (var (columnType, column) in tableConfig.ColumnMappings)
        {
            var propertyName = column.GetPropertyName(columnType);
            var property = entityType.GetProperty(propertyName);

            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Entity type '{entityType.Name}' does not have a property named '{propertyName}'. " +
                    $"Ensure the Column configuration in TableConfiguration matches the entity's property names.");
            }

            var propertyBuilder = entity.Property(propertyName);

            propertyBuilder.HasColumnName(column.Name);

            if (column.MaxLength.HasValue)
            {
                propertyBuilder.HasMaxLength(column.MaxLength.Value);
            }

            if (!string.IsNullOrWhiteSpace(column.ColumnType))
            {
                propertyBuilder.HasColumnType(column.ColumnType);
            }

            if (column.Precision.HasValue)
            {
                var (precision, scale) = column.Precision.Value;
                propertyBuilder.HasPrecision(precision, scale);
            }
        }
    }
}
