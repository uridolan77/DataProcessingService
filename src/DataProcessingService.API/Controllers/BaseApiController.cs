using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using DataProcessingService.API.DTOs;

namespace DataProcessingService.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponse<PagedResult<T>>> CreatePagedResponse<T>(
        IEnumerable<T> data,
        PaginationParameters parameters,
        int totalCount,
        string? message = null)
    {
        var items = data.ToList();
        var pageCount = (int)Math.Ceiling(totalCount / (double)parameters.PageSize);
        
        var result = new PagedResult<T>
        {
            Items = items,
            PageNumber = parameters.PageNumber,
            PageSize = parameters.PageSize,
            TotalCount = totalCount,
            TotalPages = pageCount,
            HasPreviousPage = parameters.PageNumber > 1,
            HasNextPage = parameters.PageNumber < pageCount
        };
        
        return Ok(ApiResponse<PagedResult<T>>.SuccessResponse(result, message));
    }
    
    protected ActionResult<ApiResponse<CursorPagedResult<T>>> CreateCursorPagedResponse<T, TKey>(
        IEnumerable<T> data,
        CursorPaginationParameters<TKey> parameters,
        Func<T, TKey> keySelector,
        int totalCount,
        string? message = null)
    {
        var items = data.ToList();
        
        var result = new CursorPagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            HasMore = items.Count >= parameters.Limit,
            NextCursor = items.Any() ? GetCursor(items.Last(), keySelector) : null,
            PreviousCursor = parameters.Cursor
        };
        
        return Ok(ApiResponse<CursorPagedResult<T>>.SuccessResponse(result, message));
    }
    
    private static string GetCursor<T, TKey>(T item, Func<T, TKey> keySelector)
    {
        var key = keySelector(item);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key!.ToString()!));
    }
}

public class PaginationParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;
    
    public int PageNumber { get; set; } = 1;
    
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }
    
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

public class CursorPaginationParameters<T>
{
    private const int MaxLimit = 100;
    private int _limit = 10;
    
    public string? Cursor { get; set; }
    
    public int Limit
    {
        get => _limit;
        set => _limit = value > MaxLimit ? MaxLimit : value;
    }
    
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    
    public T? GetDecodedCursor()
    {
        if (string.IsNullOrEmpty(Cursor))
            return default;
            
        try
        {
            var bytes = Convert.FromBase64String(Cursor);
            var value = System.Text.Encoding.UTF8.GetString(bytes);
            
            if (typeof(T) == typeof(Guid))
                return (T)(object)Guid.Parse(value);
                
            if (typeof(T) == typeof(int))
                return (T)(object)int.Parse(value);
                
            if (typeof(T) == typeof(long))
                return (T)(object)long.Parse(value);
                
            if (typeof(T) == typeof(DateTimeOffset))
                return (T)(object)DateTimeOffset.Parse(value);
                
            return (T)(object)value;
        }
        catch
        {
            return default;
        }
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

public class CursorPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
    public string? NextCursor { get; set; }
    public string? PreviousCursor { get; set; }
}
