using EcommerceApp.Services;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly IProductInteractionService _productInteractionService;

    public CartController(ICartService cartService, IProductInteractionService productInteractionService)
    {
        _cartService = cartService;
        _productInteractionService = productInteractionService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View(await _cartService.GetCartAsync(userId, GetStableCartSessionId(userId)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        if (!ModelState.IsValid || productId <= 0 || quantity <= 0)
            return await CartJson("Sản phẩm hoặc số lượng không hợp lệ.", false);

        if (User.IsInRole("Admin"))
        {
            return await CartJson("Tài khoản admin chỉ được xem và kiểm tra, không thể mua hàng.", false);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        try
        {
            await _cartService.AddAsync(productId, quantity, userId, GetStableCartSessionId(userId));
            await _productInteractionService.TrackAsync(productId, ProductInteractionEvents.AddToCart, HttpContext);
        }
        catch (InvalidOperationException ex)
        {
            return await CartJson(ex.Message, false);
        }
        return await CartJson("Đã thêm vào giỏ hàng.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int productId, [BindRequired] int quantity)
    {
        if (!ModelState.IsValid || productId <= 0 || quantity < 0)
            return await CartJson("Số lượng phải là số nguyên từ 0 trở lên.", false);

        if (User.IsInRole("Admin"))
        {
            return await CartJson("Tài khoản admin chỉ được xem và kiểm tra, không thể chỉnh giỏ hàng.", false);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _cartService.UpdateQuantityAsync(productId, quantity, userId, GetStableCartSessionId(userId));
        return await CartJson("Đã cập nhật giỏ hàng.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId)
    {
        if (!ModelState.IsValid || productId <= 0)
            return await CartJson("Sản phẩm không hợp lệ.", false);

        if (User.IsInRole("Admin"))
        {
            return await CartJson("Tài khoản admin chỉ được xem và kiểm tra, không thể chỉnh giỏ hàng.", false);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _cartService.RemoveAsync(productId, userId, GetStableCartSessionId(userId));
        return await CartJson("Đã xoá sản phẩm khỏi giỏ hàng.");
    }

    private async Task<IActionResult> CartJson(string message, bool success = true)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetCartAsync(userId, GetStableCartSessionId(userId));
        return Json(new
        {
            success,
            message,
            itemCount = cart.ItemCount,
            total = cart.Total.ToString("N0") + " ₫",
            grossTotal = cart.GrossTotal.ToString("N0") + " ₫",
            crossSellDiscount = cart.CrossSellDiscountAmount.ToString("N0") + " ₫",
            hasCrossSellDiscount = cart.CrossSellDiscountAmount > 0,
            items = cart.Items.Select(item => new
            {
                productId = item.ProductId,
                quantity = item.Quantity,
                stock = item.Product?.Stock ?? 0,
                unitPrice = cart.GetUnitPrice(item).ToString("N0") + " ₫",
                regularUnitPrice = cart.GetRegularUnitPrice(item).ToString("N0") + " ₫",
                hasCrossSellPrice = cart.GetUnitPrice(item) < cart.GetRegularUnitPrice(item),
                lineTotal = (cart.GetUnitPrice(item) * item.Quantity).ToString("N0") + " ₫",
                lineTotalValue = cart.GetUnitPrice(item) * item.Quantity
            }),
            groups = cart.ItemGroups.Select(group => new
            {
                key = group.Key,
                total = cart.GetGroupTotal(group).ToString("N0") + " ₫",
                componentCount = group.ComponentCount,
                quantityCount = group.QuantityCount
            }),
            crossSellSuggestions = cart.CrossSellSuggestions.Select(suggestion => new
            {
                productId = suggestion.ProductId,
                productName = suggestion.ProductName,
                anchorProductName = suggestion.AnchorProductName,
                imageUrl = suggestion.ImageUrl,
                source = suggestion.Source,
                confidencePercent = suggestion.ConfidencePercent,
                supportCount = suggestion.SupportCount,
                discountPercent = suggestion.DiscountPercent,
                hasDiscount = suggestion.HasDiscount,
                badgeText = suggestion.BadgeText,
                contextText = suggestion.ContextText,
                originalPrice = suggestion.OriginalPrice.ToString("N0") + " ₫",
                offerPrice = suggestion.OfferPrice.ToString("N0") + " ₫",
                savings = suggestion.Savings.ToString("N0") + " ₫"
            })
        });
    }

    private string GetStableCartSessionId(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Session.SetString("CartSession", "active");
        }

        return HttpContext.Session.Id;
    }
}
