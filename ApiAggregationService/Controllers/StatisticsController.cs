using ApiAggregationService.Models;
using ApiAggregationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _stats;

    public StatisticsController(IStatisticsService stats) => _stats = stats;

    /// <summary>Per-API request counts, average response time and performance buckets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderStatsSnapshot>), StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(_stats.GetAll());
}