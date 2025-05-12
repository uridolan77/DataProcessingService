using System;

namespace DataProcessingService.Core.Domain.Entities.Base;

public abstract class AuditableEntity : Entity
{
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
}
