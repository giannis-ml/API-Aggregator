namespace ApiAggregationService.Models;

public record AggregateRequest( 
    string Query,
    string? Details = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string SortBy = "date",
    string Order = "desc");