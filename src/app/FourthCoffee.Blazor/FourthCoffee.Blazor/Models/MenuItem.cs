using System.Text.Json.Serialization;

namespace FourthCoffee.Blazor.Models;

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
