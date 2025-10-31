using System.Text.Json.Serialization;

namespace FourthCoffee.Blazor.Models;

public class Customer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("loyaltyPoints")]
    public int LoyaltyPoints { get; set; }

    [JsonPropertyName("lastPurchaseDate")]
    public DateTime? LastPurchaseDate { get; set; }

    [JsonPropertyName("preferences")]
    public CustomerPreferences? Preferences { get; set; }

    [JsonPropertyName("recommendations")]
    public List<MenuItem>? Recommendations { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
