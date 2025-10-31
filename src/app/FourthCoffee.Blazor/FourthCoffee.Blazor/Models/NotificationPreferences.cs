using System.Text.Json.Serialization;

namespace FourthCoffee.Blazor.Models;

public class NotificationPreferences
{
    [JsonPropertyName("email")]
    public bool Email { get; set; }

    [JsonPropertyName("push")]
    public bool Push { get; set; }
}
