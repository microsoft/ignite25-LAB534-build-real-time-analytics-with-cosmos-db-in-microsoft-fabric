using System.Text.Json.Serialization;

namespace CustomerDemoApp.Models;

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
    public List<RecommendationGroup>? Recommendations { get; set; }

    [JsonPropertyName("registeredAt")]
    public DateTime RegisteredAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public class CustomerPreferences
{
    [JsonPropertyName("favoriteDrink")]
    public string FavoriteDrink { get; set; } = string.Empty;

    [JsonPropertyName("airport")]
    public string Airport { get; set; } = string.Empty;

    [JsonPropertyName("dietaryRestrictions")]
    public List<string> DietaryRestrictions { get; set; } = new();

    [JsonPropertyName("notificationPreferences")]
    public NotificationPreferences? NotificationPreferences { get; set; }
}

public class NotificationPreferences
{
    [JsonPropertyName("email")]
    public bool Email { get; set; }

    [JsonPropertyName("push")]
    public bool Push { get; set; }
}

public class RecommendationGroup
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

public class MenuItem
{
    [JsonPropertyName("menuItemId")]
    public string MenuItemId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class Recommendation
{
    [JsonPropertyName("menuItemId")]
    public string MenuItemId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}