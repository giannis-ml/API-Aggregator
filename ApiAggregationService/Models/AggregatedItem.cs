namespace ApiAggregationService.Models;

public record AggregatedItem(
	string Source,
	string Title,
	string? Details,
	DateTimeOffset Date,
	string? Url);