using EcommerceApp.Data;
using EcommerceApp.Hubs;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;
using System.Globalization;
using System.IO.Compression;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);

var viCulture = CultureInfo.GetCultureInfo("vi-VN");
CultureInfo.DefaultThreadCurrentCulture = viCulture;
CultureInfo.DefaultThreadCurrentUICulture = viCulture;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine("App_Data", "DataProtectionKeys");
}
if (!Path.IsPathRooted(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeysPath);
}
builder.Services
    .AddDataProtection()
    .SetApplicationName("Techvora")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null))
        .ConfigureWarnings(warnings => warnings.Ignore(
            CoreEventId.MappedEntityTypeIgnoredWarning,
            CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, MinimalUserClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".EcommerceApp.Session";
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/css",
        "application/javascript",
        "text/javascript",
        "application/json"
    });
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync("Bạn thao tác quá nhanh. Vui lòng thử lại sau.", token);
    };

    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        }));

    options.AddPolicy("review", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));

    options.AddPolicy("voucher", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.AddPolicy("ai-chat", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 18,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));
});

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<GhnOptions>(builder.Configuration.GetSection("Ghn"));
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductSpecService, ProductSpecService>();
builder.Services.AddScoped<IProductInteractionService, ProductInteractionService>();
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICrossSellService, CrossSellService>();
builder.Services.AddScoped<ICustomerSegmentService, CustomerSegmentService>();
builder.Services.AddScoped<IAbandonedCartRecoveryService, AbandonedCartRecoveryService>();
builder.Services.AddScoped<IUserNotificationService, UserNotificationService>();
builder.Services.AddScoped<IReturnWarrantyRequestService, ReturnWarrantyRequestService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IOrderEmailService, OrderEmailService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShippingService, ShippingService>();
builder.Services.AddScoped<IShippingFeeService, ShippingFeeService>();
builder.Services.AddScoped<IInvoiceService, LocalInvoiceService>();
builder.Services.AddScoped<IVnpayService, VnpayService>();
builder.Services.AddHttpClient<IGhnShippingService, GhnShippingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<IAiChatService, GeminiChatService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(35);
});
builder.Services.AddHostedService<AbandonedCartRecoveryHostedService>();

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

try
{
    app.UseExceptionHandler("/Home/Error");
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseForwardedHeaders();
    app.UseResponseCompression();
    app.UseSerilogRequestLogging();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
    var staticFileContentTypes = new FileExtensionContentTypeProvider();
    staticFileContentTypes.Mappings[".glb"] = "model/gltf-binary";
    staticFileContentTypes.Mappings[".gltf"] = "model/gltf+json";
    staticFileContentTypes.Mappings[".bin"] = "application/octet-stream";
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = staticFileContentTypes,
        OnPrepareResponse = context =>
        {
            var headers = context.Context.Response.Headers;
            headers.CacheControl = app.Environment.IsDevelopment()
                ? "no-cache"
                : "public,max-age=31536000,immutable";
        }
    });

    app.UseRouting();
    app.UseRequestLocalization(new RequestLocalizationOptions
    {
        DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(viCulture),
        SupportedCultures = new[] { viCulture },
        SupportedUICultures = new[] { viCulture }
    });

    app.UseSession();
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapGet("/health/live", () => Results.Ok(new
    {
        status = "Healthy",
        service = "Techvora",
        checkedAt = DateTimeOffset.UtcNow
    })).AllowAnonymous();

    app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return Results.Ok(new
                {
                    status = "Ready",
                    service = "Techvora",
                    checks = new { database = "Healthy" },
                    checkedAt = DateTimeOffset.UtcNow
                });
            }
        }
        catch
        {
        }

        return Results.Json(new
        {
            status = "NotReady",
            service = "Techvora",
            checks = new { database = "Unhealthy" },
            checkedAt = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }).AllowAnonymous();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    app.MapHub<AdminNotificationHub>("/hubs/admin-notifications");

    await SeedData.InitializeAsync(app.Services);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ứng dụng dừng bất thường.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
