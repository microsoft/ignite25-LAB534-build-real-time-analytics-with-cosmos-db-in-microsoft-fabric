using System.Text.Json.Serialization;

namespace FourthCoffee.Blazor.Models;

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
