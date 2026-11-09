using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Friendly handling of API failures (expired session, no permission, API offline).
    options.Filters.Add<ApiExceptionFilter>();
    // Every POST must carry a valid anti-forgery token.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHttpContextAccessor();

// Server-side session: holds the signed-in user's id/role and the API session cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services.AddScoped<UserContext>();

// The ONLY way this app reads or writes data: the Part 2 RESTful API.
builder.Services.AddHttpClient<IRaceDayApi, RaceDayApi>(client =>
    {
        var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7100/";
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        // UseCookies = false: each request forwards the cookie of the *current* user's session only.
        var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
        if (builder.Environment.IsDevelopment())
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return handler;
    });

var app = builder.Build();

// Never show raw exception details to users.
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/Status", "?code={0}");

// Disabled in Docker, where the container listens on plain HTTP.
if (app.Configuration.GetValue("UseHttpsRedirection", true)) app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
