
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
        Console.WriteLine("=== AUTH REDIRECT TO LOGIN ===");

        var returnUrl =
            context.Request.PathBase +
            context.Request.Path +
            context.Request.QueryString;

        Console.WriteLine($"RETURN URL = [{returnUrl}]");

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
// LOGIN API
// =================================================

app.MapPost(
    "/api/login",
    async (
        HttpContext context,
        IWebHostEnvironment environment) =>
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("=== API LOGIN WYWO£ANY ===");
        Console.WriteLine("========================================");


        // =================================================
        // ODCZYT BODY
        // =================================================

        LoginRequest? data;

        try
        {
            data =
                await System.Text.Json.JsonSerializer
                    .DeserializeAsync<LoginRequest>(
                        context.Request.Body);

            //Console.WriteLine("=== BODY ODCZYTANY ===");

            //Console.WriteLine(
            //    $"LOGIN = [{data?.Login}]");

            //Console.WriteLine(
            //    $"PASSWORD LENGTH = [{data?.Password?.Length}]");
        }
        catch (Exception ex)
        {
            Console.WriteLine("!!! B£¥D ODCZYTU BODY !!!");
            Console.WriteLine(ex);

            return Results.BadRequest(
                "Invalid request body.");
        }


        // =================================================
        // WALIDACJA
        // =================================================

        if (data == null)
        {
            Console.WriteLine("!!! DATA == NULL !!!");

            return Results.Unauthorized();
        }


        if (string.IsNullOrWhiteSpace(data.Login))
        {
            Console.WriteLine("!!! LOGIN JEST PUSTY !!!");

            return Results.Unauthorized();
        }


        if (string.IsNullOrWhiteSpace(data.Password))
        {
            Console.WriteLine("!!! HAS£O JEST PUSTE !!!");

            return Results.Unauthorized();
        }


        Console.WriteLine("=== WALIDACJA OK ===");


        // =================================================
        // ŒCIE¯KA ADMINS.JSON
        // =================================================

        var adminsPath =
            Path.Combine(
                environment.ContentRootPath,
                "Data",
                "admins.json");


        Console.WriteLine(
            $"ADMINS PATH = [{adminsPath}]");


        // =================================================
        // SPRAWDZENIE PLIKU
        // =================================================

        if (!File.Exists(adminsPath))
        {
            Console.WriteLine(
                "!!! ADMINS.JSON NIE ISTNIEJE !!!");

            return Results.Unauthorized();
        }


        Console.WriteLine(
            "=== ADMINS.JSON ISTNIEJE ===");


        // =================================================
        // ODCZYT PLIKU
        // =================================================

        string json;

        try
        {
            json =
                await File.ReadAllTextAsync(
                    adminsPath);

            Console.WriteLine(
                "=== ADMINS.JSON ODCZYTANY ===");

            Console.WriteLine(
                $"JSON LENGTH = [{json.Length}]");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "!!! B£¥D ODCZYTU ADMINS.JSON !!!");

            Console.WriteLine(ex);

            return Results.Unauthorized();
        }


        // =================================================
        // DESERIALIZACJA
        // =================================================

        List<AdminUser>? admins;

        try
        {
            admins =
                System.Text.Json.JsonSerializer
                    .Deserialize<List<AdminUser>>(
                        json,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

            admins ??= [];

            Console.WriteLine(
                $"=== ADMINI WCZYTANI: {admins.Count} ===");

            foreach (var a in admins)
            {
                Console.WriteLine(
                    $"ADMIN LOGIN = [{a.Login}]");

                Console.WriteLine(
                    $"HASH LENGTH = [{a.PasswordHash?.Length}]");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "!!! B£¥D DESERIALIZACJI ADMINS.JSON !!!");

            Console.WriteLine(ex);

            return Results.Unauthorized();
        }


        // =================================================
        // SZUKANIE ADMINA
        // =================================================

        Console.WriteLine(
            $"SZUKAM LOGINU = [{data.Login}]");


        var admin =
            admins.FirstOrDefault(
                x => string.Equals(
                    x.Login,
                    data.Login,
                    StringComparison.OrdinalIgnoreCase));


        if (admin == null)
        {
            Console.WriteLine(
                "!!! ADMIN NIE ZNALEZIONY !!!");

            return Results.Unauthorized();
        }


        Console.WriteLine(
            $"=== ADMIN ZNALEZIONY: [{admin.Login}] ===");


        // =================================================
        // BCRYPT
        // =================================================

        bool passwordValid;

        try
        {
            Console.WriteLine(
                "=== SPRAWDZAM BCRYPT ===");

            passwordValid =
                BCrypt.Net.BCrypt.Verify(
                    data.Password,
                    admin.PasswordHash);

            Console.WriteLine(
                $"BCRYPT RESULT = [{passwordValid}]");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "!!! B£¥D BCRYPT !!!");

            Console.WriteLine(ex);

            return Results.Unauthorized();
        }


        if (!passwordValid)
        {
            Console.WriteLine(
                "!!! HAS£O NIE PASUJE !!!");

            return Results.Unauthorized();
        }


        Console.WriteLine(
            "=== HAS£O POPRAWNE ===");


        // =================================================
        // CLAIMS
        // =================================================

        var claims = new[]
        {
            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Name,
                admin.Login),

            new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Role,
                "Admin")
        };


        Console.WriteLine(
            "=== CLAIMS UTWORZONE ===");


        // =================================================
        // IDENTITY
        // =================================================

        var identity =
            new System.Security.Claims.ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);


        var principal =
            new System.Security.Claims.ClaimsPrincipal(
                identity);


        // =================================================
        // COOKIE
        // =================================================

        try
        {
            Console.WriteLine(
                "=== TWORZÊ COOKIE ===");

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            Console.WriteLine(
                "=== COOKIE UTWORZONE ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "!!! B£¥D SIGNINASYNC !!!");

            Console.WriteLine(ex);

            return Results.StatusCode(500);
        }


        Console.WriteLine(
            "========================================");

        Console.WriteLine(
            "=== LOGIN SUKCES ===");

        Console.WriteLine(
            "========================================");

        Console.WriteLine();


        return Results.Ok();
    })
.DisableAntiforgery();


// =================================================
// API TEST
// =================================================

app.MapGet("/api/test", () =>
{
    Console.WriteLine("=== API TEST DZIA£A ===");

    return "API DZIA£A";
});


// =================================================
// LOGOUT
// =================================================

app.MapGet(
    "/api/logout",
    async (HttpContext context) =>
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
// MODELE
// =================================================

public record LoginRequest(
    string Login,
    string Password);


public class AdminUser
{
    public string Login { get; set; } = "";

    public string PasswordHash { get; set; } = "";
}

