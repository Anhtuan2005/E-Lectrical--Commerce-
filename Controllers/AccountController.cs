using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EcommerceApp.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICartService _cartService;
    private readonly IAbandonedCartRecoveryService _abandonedCartRecoveryService;
    private readonly IPasswordResetService _passwordResetService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICartService cartService,
        IAbandonedCartRecoveryService abandonedCartRecoveryService,
        IPasswordResetService passwordResetService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _cartService = cartService;
        _abandonedCartRecoveryService = abandonedCartRecoveryService;
        _passwordResetService = passwordResetService;
    }

    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null && !await _userManager.GetLockoutEnabledAsync(user))
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (user is not null)
            {
                await _cartService.MergeGuestCartAsync(user.Id, HttpContext.Session.Id);
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (!isAdmin)
                {
                    var reminder = await _abandonedCartRecoveryService.GetLoginReminderAsync(user.Id);
                    if (reminder is not null)
                    {
                        TempData["Success"] = $"Giỏ hàng của bạn đang chờ này! Dùng mã {reminder.RecoveryCode} để miễn phí vận chuyển trước {reminder.ExpiresAt.ToLocalTime():dd/MM HH:mm}.";
                    }
                }

                if (isAdmin && string.IsNullOrWhiteSpace(returnUrl))
                {
                    return Redirect("/Admin/Dashboard");
                }
            }

            return LocalRedirect(returnUrl ?? Url.Action("Index", "Home")!);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa trong 15 phút vì đăng nhập sai quá 5 lần. Vui lòng thử lại sau.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            Address = model.Address,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "User");
            await _signInManager.SignInAsync(user, isPersistent: false);
            await _cartService.MergeGuestCartAsync(user.Id, HttpContext.Session.Id);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _passwordResetService.SendResetLinkAsync(model.Email);

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    public IActionResult ResetPassword(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Link đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _passwordResetService.ResetPasswordAsync(model.Token, model.NewPassword);
        if (success)
        {
            TempData["Success"] = "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.";
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, error ?? "Có lỗi xảy ra.");
        return View(model);
    }

    [Authorize]
    [HttpGet("/account/profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            Address = user.Address
        });
    }

    [Authorize]
    [HttpPost("/account/profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        model.Email = user.Email ?? string.Empty;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.Address = model.Address;
        await _userManager.UpdateAsync(user);
        TempData["Success"] = "Đã cập nhật hồ sơ.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost("/account/delete-address")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        user.Address = null;
        await _userManager.UpdateAsync(user);
        TempData["Success"] = "Đã xóa địa chỉ đã lưu.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet("/account/change-password")]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost("/account/change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Đã đổi mật khẩu.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
