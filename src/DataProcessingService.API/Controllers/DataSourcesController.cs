using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Services;

namespace DataProcessingService.API.Controllers;

public class DataSourcesController : BaseApiController
{
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<DataSourcesController> _logger;

    public DataSourcesController(
        IDataSourceService dataSourceService,
        ILogger<DataSourcesController> logger)
    {
        _dataSourceService = dataSourceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all data sources with pagination
    /// </summary>
    /// <param name="parameters">Pagination parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged list of data sources</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DataSourceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DataSourceDto>>>> GetAllDataSources(
        [FromQuery] PaginationParameters parameters,
        CancellationToken cancellationToken)
    {
        var dataSources = await _dataSourceService.GetAllDataSourcesAsync(cancellationToken);

        // Apply sorting if specified
        var orderedSources = ApplySorting(dataSources, parameters);

        // Get total count before pagination
        var totalCount = orderedSources.Count();

        // Apply pagination
        var pagedSources = orderedSources
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize);

        // Map to DTOs
        var dtos = pagedSources.Select(MapToDto).ToList();

        return CreatePagedResponse(dtos, parameters, totalCount);
    }

    /// <summary>
    /// Gets all data sources with cursor-based pagination
    /// </summary>
    /// <param name="parameters">Cursor pagination parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cursor-paged list of data sources</returns>
    [HttpGet("cursor")]
    [ProducesResponseType(typeof(ApiResponse<CursorPagedResult<DataSourceDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CursorPagedResult<DataSourceDto>>>> GetDataSourcesWithCursor(
        [FromQuery] CursorPaginationParameters<Guid> parameters,
        CancellationToken cancellationToken)
    {
        var dataSources = await _dataSourceService.GetAllDataSourcesAsync(cancellationToken);

        // Apply cursor filtering if cursor is provided
        var cursorId = parameters.GetDecodedCursor();
        var filteredSources = dataSources;

        if (cursorId != Guid.Empty)
        {
            filteredSources = parameters.SortDescending
                ? dataSources.Where(ds => ds.Id.CompareTo(cursorId) < 0)
                : dataSources.Where(ds => ds.Id.CompareTo(cursorId) > 0);
        }

        // Apply sorting
        var orderedSources = parameters.SortDescending
            ? filteredSources.OrderByDescending(ds => ds.Id)
            : filteredSources.OrderBy(ds => ds.Id);

        // Get total count
        var totalCount = dataSources.Count();

        // Apply limit
        var limitedSources = orderedSources.Take(parameters.Limit + 1).ToList();

        // Check if there are more items
        var hasMore = limitedSources.Count > parameters.Limit;
        if (hasMore)
        {
            limitedSources = limitedSources.Take(parameters.Limit).ToList();
        }

        // Map to DTOs
        var dtos = limitedSources.Select(MapToDto).ToList();

        return CreateCursorPagedResponse(dtos, parameters, ds => ds.Id, totalCount);
    }

    /// <summary>
    /// Gets a data source by ID
    /// </summary>
    /// <param name="id">Data source ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Data source details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DataSourceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDataSourceById(Guid id, CancellationToken cancellationToken)
    {
        var dataSource = await _dataSourceService.GetDataSourceByIdAsync(id, cancellationToken);

        if (dataSource == null)
        {
            return NotFound(ApiResponse<DataSourceDto>.ErrorResponse($"Data source with ID {id} not found"));
        }

        return Ok(ApiResponse<DataSourceDto>.SuccessResponse(MapToDto(dataSource)));
    }

    /// <summary>
    /// Creates a new data source
    /// </summary>
    /// <param name="dto">Data source creation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created data source</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DataSourceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDataSource(
        [FromBody] CreateDataSourceDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataSource = await _dataSourceService.CreateDataSourceAsync(
                dto.Name,
                dto.ConnectionString,
                dto.Type,
                dto.Schema,
                dto.Properties,
                cancellationToken);

            var resultDto = MapToDto(dataSource);

            return CreatedAtAction(
                nameof(GetDataSourceById),
                new { id = resultDto.Id },
                ApiResponse<DataSourceDto>.SuccessResponse(resultDto, "Data source created successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<DataSourceDto>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Updates an existing data source
    /// </summary>
    /// <param name="id">Data source ID</param>
    /// <param name="dto">Data source update details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateDataSource(
        Guid id,
        [FromBody] UpdateDataSourceDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataSource = await _dataSourceService.GetDataSourceByIdAsync(id, cancellationToken);

            if (dataSource == null)
            {
                return NotFound(ApiResponse<DataSourceDto>.ErrorResponse($"Data source with ID {id} not found"));
            }

            await _dataSourceService.UpdateDataSourceAsync(
                id,
                dto.ConnectionString,
                dto.Schema,
                dto.IsActive,
                dto.PropertiesToAdd,
                dto.PropertiesToRemove,
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<DataSourceDto>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<DataSourceDto>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Deletes a data source
    /// </summary>
    /// <param name="id">Data source ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<DataSourceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDataSource(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _dataSourceService.DeleteDataSourceAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<DataSourceDto>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Tests the connection to a data source
    /// </summary>
    /// <param name="id">Data source ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection test result</returns>
    [HttpPost("{id}/test-connection")]
    [ProducesResponseType(typeof(ApiResponse<TestConnectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TestConnectionDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dataSource = await _dataSourceService.GetDataSourceByIdAsync(id, cancellationToken);

            if (dataSource == null)
            {
                return NotFound(ApiResponse<TestConnectionDto>.ErrorResponse($"Data source with ID {id} not found"));
            }

            bool success = await _dataSourceService.TestConnectionAsync(id, cancellationToken);

            var result = new TestConnectionDto
            {
                Success = success,
                Message = success ? "Connection successful" : "Connection failed"
            };

            return Ok(ApiResponse<TestConnectionDto>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<TestConnectionDto>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TestConnectionDto>.SuccessResponse(
                new TestConnectionDto
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                }));
        }
    }

    private static DataSourceDto MapToDto(DataSource dataSource)
    {
        return new DataSourceDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            ConnectionString = dataSource.ConnectionString,
            Type = dataSource.Type,
            Schema = dataSource.Schema,
            Properties = dataSource.Properties,
            IsActive = dataSource.IsActive,
            LastSyncTime = dataSource.LastSyncTime,
            CreatedAt = dataSource.CreatedAt,
            CreatedBy = dataSource.CreatedBy,
            LastModifiedAt = dataSource.LastModifiedAt,
            LastModifiedBy = dataSource.LastModifiedBy
        };
    }

    private static IEnumerable<DataSource> ApplySorting(
        IEnumerable<DataSource> dataSources,
        PaginationParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.SortBy))
        {
            return parameters.SortDescending
                ? dataSources.OrderByDescending(ds => ds.Name)
                : dataSources.OrderBy(ds => ds.Name);
        }

        return parameters.SortBy.ToLowerInvariant() switch
        {
            "name" => parameters.SortDescending
                ? dataSources.OrderByDescending(ds => ds.Name)
                : dataSources.OrderBy(ds => ds.Name),
            "type" => parameters.SortDescending
                ? dataSources.OrderByDescending(ds => ds.Type)
                : dataSources.OrderBy(ds => ds.Type),
            "createdat" => parameters.SortDescending
                ? dataSources.OrderByDescending(ds => ds.CreatedAt)
                : dataSources.OrderBy(ds => ds.CreatedAt),
            "lastmodifiedat" => parameters.SortDescending
                ? dataSources.OrderByDescending(ds => ds.LastModifiedAt)
                : dataSources.OrderBy(ds => ds.LastModifiedAt),
            "isactive" => parameters.SortDescending
                ? dataSources.OrderByDescending(ds => ds.IsActive)
                : dataSources.OrderBy(ds => ds.IsActive),
            _ => parameters.SortDescending
                ? dataSources.OrderByDescending(ds => ds.Name)
                : dataSources.OrderBy(ds => ds.Name)
        };
    }
}
