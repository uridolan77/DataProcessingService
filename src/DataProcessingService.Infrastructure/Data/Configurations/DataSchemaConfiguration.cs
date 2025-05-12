using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataProcessingService.Core.Domain.DataQuality;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class DataSchemaConfiguration : IEntityTypeConfiguration<DataSchema>
{
    public void Configure(EntityTypeBuilder<DataSchema> builder)
    {
        builder.HasKey(ds => ds.Id);
        
        builder.Property(ds => ds.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(ds => ds.Description)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(ds => ds.Version)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(ds => ds.IsActive)
            .IsRequired();
        
        builder.Property(ds => ds.Fields)
            .HasConversion(
                fields => JsonSerializer.Serialize(fields, new JsonSerializerOptions()),
                json => json == null 
                    ? new List<SchemaField>() 
                    : JsonSerializer.Deserialize<List<SchemaField>>(json, new JsonSerializerOptions()) 
                      ?? new List<SchemaField>());
        
        builder.HasIndex(ds => new { ds.Name, ds.Version })
            .IsUnique();
    }
}
