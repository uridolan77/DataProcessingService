using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Events;

namespace DataProcessingService.Core.Domain.Entities.Base;

public abstract class Entity
{
    private readonly List<DomainEvent> _domainEvents = new();
    
    public Guid Id { get; protected set; } = Guid.NewGuid();
    
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;
        
        if (ReferenceEquals(this, other))
            return true;
        
        if (GetType() != other.GetType())
            return false;
        
        return Id == other.Id;
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    
    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;
        
        if (left is null || right is null)
            return false;
        
        return left.Equals(right);
    }
    
    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
