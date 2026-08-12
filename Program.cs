using Microsoft.AspNetCore.Authentication.Cookies;
using Darge.Web.Services;

var builder =
    WebApplication.CreateBuilder(args);


builder.Services
    .AddControllersWithViews();

builder.Services
    .AddHttpClient<IDargeAiService, DargeAiService>();


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


app.UseStaticFiles();

app.UseRouting();


app.UseAuthentication();

app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}"
);


app.Run();