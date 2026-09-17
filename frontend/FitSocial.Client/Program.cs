using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using FitSocial.Client;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Http;
using FitSocial.Client.Services.Posts;
using FitSocial.Client.Services.Realtime;
using FitSocial.Client.Services.Sports;
using FitSocial.Client.Services.Files;
using FitSocial.Client.Services.Payment;
using FitSocial.Client.Services.Coaches;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// LocalStorage
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<ITokenStorage, BrowserTokenStorage>();

// Authentication & Authorization
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// HttpClient configured with Backend API Base URL
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7200/api/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Core Services
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<GoogleSignInService>();
builder.Services.AddScoped<ISportService, SportService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<PendingCoachRegistration>();
builder.Services.AddScoped<IPaymentService, PaymentApiService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<INotificationHubClient, NotificationHubClient>();
builder.Services.AddScoped<ICoachService, CoachService>();

await builder.Build().RunAsync();
