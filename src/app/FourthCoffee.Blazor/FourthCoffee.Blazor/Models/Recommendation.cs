using System.Text.Json.Serialization;

namespace FourthCoffee.Blazor.Models;

public class Recommendation
{
    [JsonPropertyName("recommendationId")]
    public string RecommendationId { get; set; } = string.Empty;

    [JsonPropertyName("menuItems")]
    public List<MenuItem>? MenuItems { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("generatedAt")]
    public DateTime? GeneratedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}