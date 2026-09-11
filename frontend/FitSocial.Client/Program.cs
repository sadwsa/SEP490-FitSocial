using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using FitSocial.Client;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Http;
using FitSocial.Client.Services.Posts;
using FitSocial.Client.Services.Realtime;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Authentication & Authorization
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// HttpClient configured with Backend API Base URL
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7200/api/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Core Services
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<INotificationHubClient, NotificationHubClient>();

await builder.Build().RunAsync();
