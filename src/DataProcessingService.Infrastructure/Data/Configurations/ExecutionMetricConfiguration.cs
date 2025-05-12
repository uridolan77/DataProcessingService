using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class ExecutionMetricConfiguration : IEntityTypeConfiguration<ExecutionMetric>
{
    public void Configure(EntityTypeBuilder<ExecutionMetric> builder)
    {
        builder.HasKey(em => em.Id);
        
        builder.Property(em => em.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(em => em.Value)
            .IsRequired();
        
        builder.Property(em => em.Type)
            .IsRequired();
        
        builder.Property(em => em.Timestamp)
            .IsRequired();
        
        builder.Property<Guid>("PipelineExecutionId")
            .IsRequired();
    }
}
