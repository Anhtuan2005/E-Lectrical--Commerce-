using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface ICrossSellService
{
    Task<IReadOnlyDictionary<int, decimal>> GetEligibleUnitPricesAsync(IEnumerable<CartItem> cartItems, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrossSellSuggestionViewModel>> GetSuggestionsAsync(IEnumerable<CartItem> cartItems, int take = 4, CancellationToken cancellationToken = default);
}
