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
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorization();


// =================================================
// BLAZOR
// =================================================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();


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
// BLAZOR
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
                "Andrzej")
        };

        var identity =
            new System.Security.Claims.ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

        var principal =
            new System.Security.Claims.ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        return Results.Ok();
    }

    return Results.Unauthorized();
})
.DisableAntiforgery();




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