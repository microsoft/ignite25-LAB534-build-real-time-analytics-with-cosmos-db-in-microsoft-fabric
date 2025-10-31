using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FourthCoffee.Blazor.Interfaces;
using FourthCoffee.Blazor.Models;
using FourthCoffee.Blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace FourthCoffee.Blazor.Components.Pages
{
    public partial class Home
    {
        private const int MaxRecommendationsToDisplay = 6;
        private const int InitialCustomerBatchSize = 40;
        private const int SearchResultBatchSize = 25;
        private const int MinimumRemoteSearchLength = 3;

        private List<Customer>? customers;
        private List<Customer> filteredCustomers = new();
        private Customer? selectedCustomer;
        private bool isLoading = true;
        private string errorMessage = string.Empty;
        private IReadOnlyList<RecommendationGroup>? groupedRecommendations;
        private string customerSearchText = string.Empty;
        private bool isCustomerDropdownOpen;
        private int highlightedCustomerIndex = -1;
        private CancellationTokenSource? customerSearchCts;

        [Inject]
        protected ICustomerService CustomerService { get; set; } = default!;

        [Inject]
        protected ILogger<Home> Logger { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await LoadCustomersAsync();
        }

        protected bool UsesLocalData => CustomerService is LocalJsonCustomerService;

        protected string SelectedCustomerFirstName => selectedCustomer?.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? selectedCustomer?.Name
            ?? string.Empty;

        protected IReadOnlyList<Customer> FilteredCustomers => filteredCustomers;

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

        private async Task LoadCustomersAsync()
        {
            CancelPendingCustomerSearch();
            isLoading = true;
            errorMessage = string.Empty;
            selectedCustomer = null;
            groupedRecommendations = null;
            customerSearchText = string.Empty;
            isCustomerDropdownOpen = false;
            highlightedCustomerIndex = -1;
            filteredCustomers = new List<Customer>();

            try
            {
                customers = await CustomerService.GetCustomersAsync(InitialCustomerBatchSize);

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
                ApplyCustomerFilter(resetHighlight: true);
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task OnCustomerSearchInput(ChangeEventArgs args)
        {
            customerSearchText = args.Value?.ToString() ?? string.Empty;
            isCustomerDropdownOpen = true;
            ApplyCustomerFilter(resetHighlight: true);
            await EnsureRemoteCustomerResultsAsync();
        }

        private async Task OnCustomerSearchKeyDown(KeyboardEventArgs args)
        {
            if (args.Key == "ArrowDown")
            {
                if (filteredCustomers.Count == 0)
                {
                    return;
                }

                isCustomerDropdownOpen = true;
                highlightedCustomerIndex = highlightedCustomerIndex < filteredCustomers.Count - 1 && highlightedCustomerIndex >= 0
                    ? highlightedCustomerIndex + 1
                    : 0;
            }
            else if (args.Key == "ArrowUp")
            {
                if (filteredCustomers.Count == 0)
                {
                    return;
                }

                isCustomerDropdownOpen = true;
                highlightedCustomerIndex = highlightedCustomerIndex > 0
                    ? highlightedCustomerIndex - 1
                    : filteredCustomers.Count - 1;
            }
            else if (args.Key == "Enter")
            {
                if (filteredCustomers.Count == 0)
                {
                    if (!UsesLocalData)
                    {
                        await EnsureRemoteCustomerResultsAsync();

                        if (filteredCustomers.Count == 0)
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                var index = highlightedCustomerIndex >= 0 ? highlightedCustomerIndex : 0;
                await SelectCustomerAsync(filteredCustomers[index].Id);
                return;
            }
            else if (args.Key == "Escape")
            {
                isCustomerDropdownOpen = false;
            }

            StateHasChanged();
        }

        private void OpenCustomerDropdown()
        {
            if (!isCustomerDropdownOpen)
            {
                isCustomerDropdownOpen = true;
            }

            if (filteredCustomers.Count == 0)
            {
                ApplyCustomerFilter(resetHighlight: true);

                if (filteredCustomers.Count == 0)
                {
                    _ = EnsureRemoteCustomerResultsAsync();
                }
            }

            if (highlightedCustomerIndex < 0 && filteredCustomers.Count > 0)
            {
                highlightedCustomerIndex = 0;
            }
        }

        private void ToggleCustomerDropdown()
        {
            isCustomerDropdownOpen = !isCustomerDropdownOpen;

            if (isCustomerDropdownOpen && filteredCustomers.Count == 0)
            {
                ApplyCustomerFilter(resetHighlight: true);

                if (filteredCustomers.Count == 0)
                {
                    _ = EnsureRemoteCustomerResultsAsync();
                }
            }

            if (isCustomerDropdownOpen && highlightedCustomerIndex < 0 && filteredCustomers.Count > 0)
            {
                highlightedCustomerIndex = 0;
            }
        }

        private void ClearCustomerSearch()
        {
            CancelPendingCustomerSearch();
            customerSearchText = string.Empty;
            ApplyCustomerFilter(resetHighlight: true);
            isCustomerDropdownOpen = true;

            if (selectedCustomer != null)
            {
                var index = filteredCustomers.FindIndex(c => c.Id == selectedCustomer.Id);

                if (index >= 0)
                {
                    highlightedCustomerIndex = index;
                }
            }
        }

        private async Task SelectCustomerAsync(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return;
            }

            try
            {
                selectedCustomer = customers?.FirstOrDefault(c => c.Id == customerId)
                    ?? await CustomerService.GetCustomerByIdAsync(customerId);
            }
            catch (Exception ex)
            {
                errorMessage = $"Error loading customer details: {ex.Message}";
                Logger.LogError(ex, "Error loading customer {CustomerId}", customerId);
                return;
            }
            finally
            {
                groupedRecommendations = null;
            }

            if (selectedCustomer == null)
            {
                return;
            }

            if (customers != null)
            {
                var index = customers.FindIndex(c => c.Id == selectedCustomer.Id);

                if (index >= 0)
                {
                    customers[index] = selectedCustomer;
                }
            }

            customerSearchText = selectedCustomer.Name ?? string.Empty;
            isCustomerDropdownOpen = false;
            ApplyCustomerFilter(resetHighlight: false);

            var highlightedIndex = filteredCustomers.FindIndex(c => c.Id == selectedCustomer.Id);

            if (highlightedIndex >= 0)
            {
                highlightedCustomerIndex = highlightedIndex;
            }

            StateHasChanged();
        }

        private void ApplyCustomerFilter(bool resetHighlight)
        {
            if (customers == null || customers.Count == 0)
            {
                filteredCustomers = new List<Customer>();
                highlightedCustomerIndex = -1;
                return;
            }

            var query = customerSearchText?.Trim();
            IEnumerable<Customer> queryable = customers;

            if (!string.IsNullOrWhiteSpace(query))
            {
                queryable = customers.Where(c =>
                    (!string.IsNullOrWhiteSpace(c.Name) && c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(c.Email) && c.Email.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
            }

            filteredCustomers = queryable
                .OrderBy(c => c.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (filteredCustomers.Count == 0)
            {
                highlightedCustomerIndex = -1;
                return;
            }

            if (resetHighlight)
            {
                highlightedCustomerIndex = 0;
                return;
            }

            if (selectedCustomer != null)
            {
                var selectedIndex = filteredCustomers.FindIndex(c => c.Id == selectedCustomer.Id);

                if (selectedIndex >= 0)
                {
                    highlightedCustomerIndex = selectedIndex;
                    return;
                }
            }

            if (highlightedCustomerIndex < 0 || highlightedCustomerIndex >= filteredCustomers.Count)
            {
                highlightedCustomerIndex = 0;
            }
        }

        private static string GetCustomerOptionId(int index) => $"customer-option-{index}";

        private string? GetActiveOptionId()
        {
            return isCustomerDropdownOpen && highlightedCustomerIndex >= 0 && highlightedCustomerIndex < filteredCustomers.Count
                ? GetCustomerOptionId(highlightedCustomerIndex)
                : null;
        }

        private async Task EnsureRemoteCustomerResultsAsync()
        {
            if (UsesLocalData)
            {
                return;
            }

            var query = customerSearchText?.Trim();

            if (string.IsNullOrWhiteSpace(query) || query.Length < MinimumRemoteSearchLength)
            {
                return;
            }

            if (filteredCustomers.Count > 0)
            {
                return;
            }

            CancelPendingCustomerSearch();

            customerSearchCts = new CancellationTokenSource();
            var token = customerSearchCts.Token;

            try
            {
                var results = await CustomerService.SearchCustomersAsync(query, SearchResultBatchSize, token);

                if (token.IsCancellationRequested || results?.Any() != true)
                {
                    return;
                }

                customers ??= new List<Customer>();
                var comparer = StringComparer.OrdinalIgnoreCase;
                var existingIds = new HashSet<string>(customers
                    .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                    .Select(c => c.Id!), comparer);

                foreach (var customer in results)
                {
                    if (customer == null)
                    {
                        continue;
                    }

                    var id = customer.Id;

                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        if (existingIds.Contains(id))
                        {
                            var index = customers.FindIndex(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

                            if (index >= 0)
                            {
                                customers[index] = customer;
                            }
                        }
                        else
                        {
                            customers.Add(customer);
                            existingIds.Add(id);
                        }
                    }
                    else
                    {
                        customers.Add(customer);
                    }
                }

                ApplyCustomerFilter(resetHighlight: true);
                highlightedCustomerIndex = filteredCustomers.Count > 0 ? 0 : -1;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                errorMessage = $"Unable to search customers: {ex.Message}";
                Logger.LogError(ex, "Error searching customers with query {Query}", query);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    await InvokeAsync(StateHasChanged);
                }
            }
        }

        private void CancelPendingCustomerSearch()
        {
            if (customerSearchCts == null)
            {
                return;
            }

            try
            {
                if (!customerSearchCts.IsCancellationRequested)
                {
                    customerSearchCts.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                customerSearchCts.Dispose();
                customerSearchCts = null;
            }
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

        public void Dispose()
        {
            CancelPendingCustomerSearch();
        }

    }
}
