using KatalogCzesci.Components;
using KatalogCzesci.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);


// =================================================
// SERVICES
// =================================================

builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<AdSearchService>();


// =================================================
// AUTHENTICATION
// =================================================

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.Cookie.Name = "KatalogCzesci.Auth";

    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";

    options.Events.OnRedirectToLogin = context =>
    {
        var returnUrl =
            context.Request.PathBase +
            context.Request.Path +
            context.Request.QueryString;

        var loginUrl =
            "/login?returnUrl=" +
            Uri.EscapeDataString(returnUrl);

        context.Response.Redirect(loginUrl);

        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();


// =================================================
// BLAZOR
// =================================================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();


var app = builder.Build();


// =================================================
// HTTP REQUEST PIPELINE
// =================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();


// =================================================
// AUTHENTICATION / AUTHORIZATION
// =================================================

app.UseAuthentication();
app.UseAuthorization();


// =================================================
// ANTIFORGERY
// =================================================

app.UseAntiforgery();


// =================================================
// LOGIN
// =================================================

app.MapPost("/api/login", async (HttpContext context) =>
{
    var data =
        await System.Text.Json.JsonSerializer
            .DeserializeAsync<LoginRequest>(
                context.Request.Body);


    Console.WriteLine(
        $"LOGIN TEST: [{data?.Username}] / [{data?.Password}]");


    if (data?.Username == "Andrzej" &&
        data.Password == "test123")
    {
        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Name,
                "Andrzej"),

            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Role,
                "Admin")
        };


        var identity =
            new System.Security.Claims.ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);


        var principal =
            new System.Security.Claims.ClaimsPrincipal(
                identity);


        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);


        return Results.Ok();
    }


    return Results.Unauthorized();
})
.DisableAntiforgery();


app.MapGet("/api/test", () =>
{
    Console.WriteLine("=== API TEST DZIA£A ===");

    return "API DZIA£A";
});


// =================================================
// LOGOUT
// =================================================

app.MapGet("/api/logout", async (HttpContext context) =>
{
    Console.WriteLine("=== LOGOUT ===");

    await context.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    context.Response.Cookies.Delete(
        "KatalogCzesci.Auth");

    return Results.Redirect("/");
});


// =================================================
// BLAZOR
// =================================================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


// =================================================
// RUN
// =================================================

app.Run();


// =================================================
// LOGIN MODEL
// =================================================

public record LoginRequest(
    string Username,
    string Password);