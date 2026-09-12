using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface ISmartPcBuildService
{
    Task<Dictionary<string, List<Product>>> GetSlotProductsAsync();
    Task<List<Product>> GetProductsForSlotAsync(string slot, int take);
    IReadOnlyList<SmartBuildGoalViewModel> GetGoals();
    Task<SmartBuildResponse> BuildAsync(SmartBuildRequest? request);
}
