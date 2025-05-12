using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataMaskingService
{
    Task<IEnumerable<ExpandoObject>> MaskDataAsync(
        IEnumerable<ExpandoObject> data,
        Dictionary<string, MaskingRule> maskingRules,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> MaskDataAsync(
        IEnumerable<ExpandoObject> data,
        MaskingProfile maskingProfile,
        CancellationToken cancellationToken = default);
    
    Task<MaskingProfile> CreateMaskingProfileAsync(
        string name,
        string description,
        Dictionary<string, MaskingRule> maskingRules,
        CancellationToken cancellationToken = default);
    
    Task<MaskingProfile?> GetMaskingProfileAsync(
        string name,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<MaskingProfile>> GetAllMaskingProfilesAsync(
        CancellationToken cancellationToken = default);
    
    Task UpdateMaskingProfileAsync(
        string name,
        Dictionary<string, MaskingRule> maskingRules,
        CancellationToken cancellationToken = default);
    
    Task DeleteMaskingProfileAsync(
        string name,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> DetectSensitiveDataAsync(
        IEnumerable<ExpandoObject> data,
        SensitiveDataType[] typesToDetect,
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, SensitiveDataType>> SuggestMaskingRulesAsync(
        IEnumerable<ExpandoObject> sampleData,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> MaskDataFromSourceAsync(
        DataSource source,
        string tableName,
        MaskingProfile maskingProfile,
        CancellationToken cancellationToken = default);
}

public class MaskingRule
{
    public MaskingType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    public static MaskingRule Redact() => new()
    {
        Type = MaskingType.Redact
    };
    
    public static MaskingRule PartialMask(int preserveStartChars = 0, int preserveEndChars = 0, char maskChar = '*') => new()
    {
        Type = MaskingType.PartialMask,
        Parameters = new Dictionary<string, object>
        {
            ["preserveStartChars"] = preserveStartChars,
            ["preserveEndChars"] = preserveEndChars,
            ["maskChar"] = maskChar
        }
    };
    
    public static MaskingRule Tokenize(bool preserveLength = true, bool preserveFormat = true) => new()
    {
        Type = MaskingType.Tokenize,
        Parameters = new Dictionary<string, object>
        {
            ["preserveLength"] = preserveLength,
            ["preserveFormat"] = preserveFormat
        }
    };
    
    public static MaskingRule Randomize(string? domain = null) => new()
    {
        Type = MaskingType.Randomize,
        Parameters = new Dictionary<string, object>
        {
            ["domain"] = domain ?? string.Empty
        }
    };
    
    public static MaskingRule Hash(string salt = "", bool preserveLength = false) => new()
    {
        Type = MaskingType.Hash,
        Parameters = new Dictionary<string, object>
        {
            ["salt"] = salt,
            ["preserveLength"] = preserveLength
        }
    };
    
    public static MaskingRule DateShift(int minDays = -30, int maxDays = 30, bool preserveDay = false) => new()
    {
        Type = MaskingType.DateShift,
        Parameters = new Dictionary<string, object>
        {
            ["minDays"] = minDays,
            ["maxDays"] = maxDays,
            ["preserveDay"] = preserveDay
        }
    };
    
    public static MaskingRule NumberVariance(double minPercent = -10, double maxPercent = 10) => new()
    {
        Type = MaskingType.NumberVariance,
        Parameters = new Dictionary<string, object>
        {
            ["minPercent"] = minPercent,
            ["maxPercent"] = maxPercent
        }
    };
    
    public static MaskingRule Custom(Dictionary<string, object> parameters) => new()
    {
        Type = MaskingType.Custom,
        Parameters = parameters
    };
}

public enum MaskingType
{
    Redact,
    PartialMask,
    Tokenize,
    Randomize,
    Hash,
    DateShift,
    NumberVariance,
    Custom
}

public class MaskingProfile
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public Dictionary<string, MaskingRule> Rules { get; set; } = new();
}

public enum SensitiveDataType
{
    PersonName,
    EmailAddress,
    PhoneNumber,
    Address,
    CreditCardNumber,
    SocialSecurityNumber,
    IpAddress,
    Password,
    DateOfBirth,
    FinancialData,
    HealthData,
    Custom
}
