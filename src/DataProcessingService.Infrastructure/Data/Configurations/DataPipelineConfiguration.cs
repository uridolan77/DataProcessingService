using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class DataPipelineConfiguration : IEntityTypeConfiguration<DataPipeline>
{
    public void Configure(EntityTypeBuilder<DataPipeline> builder)
    {
        builder.HasKey(dp => dp.Id);
        
        builder.Property(dp => dp.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(dp => dp.Description)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(dp => dp.Type)
            .IsRequired();
        
        builder.Property(dp => dp.Status)
            .IsRequired();
        
        builder.Property(dp => dp.Schedule)
            .HasConversion(
                s => JsonSerializer.Serialize(s, new JsonSerializerOptions()),
                s => s == null 
                    ? null
                    : JsonSerializer.Deserialize<ExecutionSchedule>(s, new JsonSerializerOptions()));
        
        builder.Property(dp => dp.TransformationRules)
            .HasConversion(
                tr => JsonSerializer.Serialize(tr, new JsonSerializerOptions()),
                tr => tr == null 
                    ? TransformationRules.Empty
                    : JsonSerializer.Deserialize<TransformationRules>(tr, new JsonSerializerOptions()) 
                      ?? TransformationRules.Empty);
        
        builder.HasOne(dp => dp.Source)
            .WithMany(ds => ds.DataPipelines)
            .HasForeignKey(dp => dp.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(dp => dp.Destination)
            .WithMany()
            .HasForeignKey(dp => dp.DestinationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(dp => dp.Executions)
            .WithOne(pe => pe.Pipeline)
            .HasForeignKey(pe => pe.PipelineId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(dp => dp.Name)
            .IsUnique();
    }
}
