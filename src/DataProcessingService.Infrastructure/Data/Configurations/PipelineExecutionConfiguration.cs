using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class PipelineExecutionConfiguration : IEntityTypeConfiguration<PipelineExecution>
{
    public void Configure(EntityTypeBuilder<PipelineExecution> builder)
    {
        builder.HasKey(pe => pe.Id);
        
        builder.Property(pe => pe.StartTime)
            .IsRequired();
        
        builder.Property(pe => pe.Status)
            .IsRequired();
        
        builder.Property(pe => pe.ProcessedRecords)
            .IsRequired();
        
        builder.Property(pe => pe.FailedRecords)
            .IsRequired();
        
        builder.HasOne(pe => pe.Pipeline)
            .WithMany(dp => dp.Executions)
            .HasForeignKey(pe => pe.PipelineId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(pe => pe.Metrics)
            .WithOne()
            .HasForeignKey("PipelineExecutionId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
