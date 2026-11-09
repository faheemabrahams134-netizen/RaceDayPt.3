using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The "Testing" environment is used by the unit tests, which register an in-memory database themselves.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddSingleton<IPasswordService, PasswordService>();

// Azure Blob Storage (or the Azurite emulator). All image uploads/downloads go through the API.
builder.Services.AddSingleton(_ => new BlobServiceClient(
    builder.Configuration["AzureBlob:ConnectionString"] ?? "UseDevelopmentStorage=true"));
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

// Session management: the server issues an HttpOnly cookie after login that carries the user's id and role.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "RaceDay.Session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // This is an API: answer with status codes instead of redirecting to a login page.
        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "Run, walk and cycle event management. Call POST /api/auth/login first - " +
                      "the session cookie is then sent automatically by Swagger UI."
    });
    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlFile)) c.IncludeXmlComments(xmlFile);
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await InitialiseDatabaseAsync(app);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.RoutePrefix = "swagger";
});

// Disabled in Docker, where the API listens on plain HTTP behind the MVC container.
if (app.Configuration.GetValue("UseHttpsRedirection", true)) app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// SQL Server can take a while to accept connections when it starts alongside the API (e.g. docker compose).
static async Task InitialiseDatabaseAsync(WebApplication app)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            if (app.Configuration.GetValue<bool>("Seed:DemoData"))
                await DemoDataSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordService>());
            return;
        }
        catch (Exception ex) when (attempt < 12)
        {
            app.Logger.LogWarning(ex, "Database not ready (attempt {Attempt}). Retrying in 5 seconds...", attempt);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

// Makes Program visible to the test project (WebApplicationFactory<Program>).
public partial class Program { }
