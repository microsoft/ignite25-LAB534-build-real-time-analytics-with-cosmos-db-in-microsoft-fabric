using System.Globalization;
using System.Linq;
using FourthCoffee.Blazor.Interfaces;
using FourthCoffee.Blazor.Models;
using FourthCoffee.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FourthCoffee.Blazor.Components.Pages
{
    public partial class Home
    {
        private const int MaxRecommendationsToDisplay = 6;

        private List<Customer>? customers;
        private Customer? selectedCustomer;
        private bool isLoading = true;
        private string errorMessage = string.Empty;
        private IReadOnlyList<RecommendationGroup>? groupedRecommendations;

        [Inject]
        protected ICustomerService CustomerService { get; set; } = default!;

        [Inject]
        protected ILogger<Home> Logger { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await LoadRandomCustomers();
        }

        protected bool UsesLocalData => CustomerService is LocalJsonCustomerService;

        protected string SelectedCustomerFirstName => selectedCustomer?.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? selectedCustomer?.Name
            ?? string.Empty;

        protected IReadOnlyList<RecommendationGroup> GroupedRecommendations => groupedRecommendations ??= BuildRecommendationGroups();

        protected bool HasRecommendationGroups => GroupedRecommendations.Count > 0;

        protected static string? GetGeneratedLabel(Recommendation recommendation)
        {
            if (recommendation.GeneratedAt is DateTime generatedAt)
            {
                return $"Generated {generatedAt.ToString("t", CultureInfo.CurrentCulture)}";
            }

            return null;
        }

        protected static string? GetExpiresLabel(Recommendation recommendation)
        {
            if (recommendation.ExpiresAt is DateTime expiresAt)
            {
                return $"Expires {expiresAt.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture)}";
            }

            return null;
        }

        protected static string? GetSourceLabel(Recommendation recommendation)
        {
            return string.IsNullOrWhiteSpace(recommendation.Source)
                ? null
                : recommendation.Source.Trim();
        }

        private async Task LoadRandomCustomers()
        {
            isLoading = true;
            errorMessage = string.Empty;
            selectedCustomer = null;
            groupedRecommendations = null;

            try
            {
                customers = await CustomerService.GetRandomCustomersAsync(5);

                if (customers?.Any() != true)
                {
                    errorMessage = "No customers found. Please ensure your customer data is available.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Unable to load customers: {ex.Message}";
                Logger.LogError(ex, "Error loading customers");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task OnCustomerSelected(ChangeEventArgs e)
        {
            var customerId = e.Value?.ToString();

            if (string.IsNullOrEmpty(customerId))
            {
                selectedCustomer = null;
                groupedRecommendations = null;
                return;
            }

            try
            {
                selectedCustomer = customers?.FirstOrDefault(c => c.Id == customerId);

                if (selectedCustomer == null)
                {
                    selectedCustomer = await CustomerService.GetCustomerByIdAsync(customerId);
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Error loading customer details: {ex.Message}";
                Logger.LogError(ex, "Error loading customer {CustomerId}", customerId);
            }

            groupedRecommendations = null;
            StateHasChanged();
        }

        private IReadOnlyList<RecommendationGroup> BuildRecommendationGroups()
        {
            if (selectedCustomer?.Recommendations?.Any() != true)
            {
                return Array.Empty<RecommendationGroup>();
            }

            var topRecommendations = selectedCustomer.Recommendations
                .OrderByDescending(rec => rec.GeneratedAt ?? DateTime.MinValue)
                .ThenByDescending(rec => rec.Score)
                .Take(MaxRecommendationsToDisplay)
                .ToList();

            return topRecommendations
                .GroupBy(rec => rec.GeneratedAt?.Date)
                .Select(group => new RecommendationGroup(
                    group.Key,
                    group.OrderByDescending(rec => rec.GeneratedAt ?? DateTime.MinValue).ToList()))
                .OrderByDescending(group => group.Date ?? DateTime.MinValue)
                .ToList();
        }

        protected sealed record RecommendationGroup(DateTime? Date, IReadOnlyList<Recommendation> Recommendations)
        {
            public string DateLabel => Date.HasValue
                ? Date.Value.ToString("MMMM dd, yyyy", CultureInfo.CurrentCulture)
                : "Undated recommendations";
        }

    }
}
