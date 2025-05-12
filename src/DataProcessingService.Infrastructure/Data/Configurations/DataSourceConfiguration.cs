using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.HasKey(ds => ds.Id);
        
        builder.Property(ds => ds.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(ds => ds.ConnectionString)
            .IsRequired();
        
        builder.Property(ds => ds.Type)
            .IsRequired();
        
        builder.Property(ds => ds.Schema)
            .HasMaxLength(100);
        
        builder.Property(ds => ds.IsActive)
            .IsRequired();
        
        builder.Property(ds => ds.LastSyncTime)
            .IsRequired();
        
        builder.Property(ds => ds.Properties)
            .HasConversion(
                p => JsonSerializer.Serialize(p, new JsonSerializerOptions()),
                p => p == null 
                    ? new Dictionary<string, string>() 
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(p, new JsonSerializerOptions()) 
                      ?? new Dictionary<string, string>());
        
        builder.HasMany(ds => ds.DataPipelines)
            .WithOne(dp => dp.Source)
            .HasForeignKey(dp => dp.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(ds => ds.Name)
            .IsUnique();
    }
}
