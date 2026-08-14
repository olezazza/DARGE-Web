using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Darge.Web.Services;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews();

builder.Services
    .AddHttpClient<IDargeAiService, DargeAiService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
    });

builder.Services
    .AddSingleton<ILocalizationService, LocalizationService>();

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme
    )
    .AddCookie(options =>
    {
        options.LoginPath =
            "/Account/Login";

        options.AccessDeniedPath =
            "/Account/Login";

        options.Cookie.Name =
            "DargeAuth";
    });


var app =
    builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Home/Error"
    );

    app.UseHsts();
}

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("ka")
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});


app.UseStaticFiles();

app.UseRouting();


app.UseAuthentication();

app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}"
);


// Initialize database schema tables (ai_conversations, ai_messages, etc.)
try
{
    Darge.Web.Data.Database.InitializeTables();
}
catch (Exception ex)
{
    Console.WriteLine($"[Database Warning] Schema initialization failed: {ex.Message}");
}

app.Run();