using System;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.ValueObjects;

public class ExecutionSchedule
{
    public ScheduleType Type { get; private set; }
    public int? Interval { get; private set; }
    public string? CronExpression { get; private set; }
    
    private ExecutionSchedule() { }
    
    private ExecutionSchedule(ScheduleType type, int? interval, string? cronExpression)
    {
        Type = type;
        Interval = interval;
        CronExpression = cronExpression;
    }
    
    public static ExecutionSchedule CreateInterval(int minutes)
    {
        if (minutes <= 0)
            throw new ArgumentException("Interval must be greater than zero", nameof(minutes));
            
        return new ExecutionSchedule(ScheduleType.Interval, minutes, null);
    }
    
    public static ExecutionSchedule CreateDaily(int hour, int minute)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
            
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
            
        string cronExpression = $"0 {minute} {hour} * * *";
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public static ExecutionSchedule CreateWeekly(DayOfWeek dayOfWeek, int hour, int minute)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
            
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
            
        int cronDayOfWeek = (int)dayOfWeek; // In cron, Sunday is 0
        string cronExpression = $"0 {minute} {hour} * * {cronDayOfWeek}";
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public static ExecutionSchedule CreateMonthly(int dayOfMonth, int hour, int minute)
    {
        if (dayOfMonth < 1 || dayOfMonth > 31)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31");
            
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
            
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
            
        string cronExpression = $"0 {minute} {hour} {dayOfMonth} * *";
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public static ExecutionSchedule CreateCustomCron(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be empty", nameof(cronExpression));
            
        // Here you might want to validate the cron expression format
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public DateTimeOffset CalculateNextExecution(DateTimeOffset fromTime)
    {
        return Type switch
        {
            ScheduleType.Interval => fromTime.AddMinutes(Interval!.Value),
            ScheduleType.Cron => CalculateNextCronExecution(fromTime),
            _ => throw new NotImplementedException($"Schedule type {Type} is not supported")
        };
    }
    
    private DateTimeOffset CalculateNextCronExecution(DateTimeOffset fromTime)
    {
        // This would normally use a library like Cronos to parse cron expressions
        // For simplicity, we'll just add a day as a placeholder
        // In a real implementation, this would properly parse and calculate based on the cron expression
        return fromTime.AddDays(1);
    }
}
