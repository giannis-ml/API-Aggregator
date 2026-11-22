using ApiAggregationService.Models;
using ApiAggregationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AggregateController : ControllerBase
{
    private readonly IAggregatorService _aggregator;

    public AggregateController(IAggregatorService aggregator) => _aggregator = aggregator;

    /// <summary>Aggregated data from all providers, with optional filtering and sorting.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AggregateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] AggregateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("The 'query' parameter is required.");

        return Ok(await _aggregator.GetAsync(request, ct));
    }
}