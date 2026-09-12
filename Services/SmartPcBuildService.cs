using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public sealed class SmartPcBuildService(AppDbContext db) : ISmartPcBuildService
{
    public IReadOnlyList<SmartBuildGoalViewModel> GetGoals() => SmartPcBuildEngine.GetGoals();

    public async Task<SmartBuildResponse> BuildAsync(SmartBuildRequest? request) =>
        SmartPcBuildEngine.Build(request, await LoadInventoryAsync());

    public async Task<Dictionary<string, List<Product>>> GetSlotProductsAsync()
    {
        var inventory = await LoadInventoryAsync();
        return PcSlots.All.ToDictionary(slot => slot,
            slot => SmartPcBuildEngine.SelectProductsForSlot(inventory, slot, 10));
    }

    public async Task<List<Product>> GetProductsForSlotAsync(string slot, int take) =>
        SmartPcBuildEngine.SelectProductsForSlot(await LoadInventoryAsync(), slot, take);

    private Task<List<Product>> LoadInventoryAsync() => db.Products.AsNoTracking()
        .Include(product => product.Category).Include(product => product.Images)
        .Where(product => product.Stock > 0).ToListAsync();
}
