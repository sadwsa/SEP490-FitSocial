using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Users;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

namespace FitSocial.Client.Pages;

public partial class UserProfile : ComponentBase
{
    [Parameter] public string? UserId { get; set; }

    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private FitSocial.Client.Services.TrainingPackages.ITrainingPackageService PackageService { get; set; } = default!;
    [Inject] private FitSocial.Client.Services.Cart.ICartService CartService { get; set; } = default!;

    private OtherUserProfileModel? profile;
    private bool isLoading = true;
    private string? errorMessage;
    private bool isUnauthorized;

    // Header & Viewer State (Current authenticated user)
    private string headerSearchTerm = string.Empty;
    private string headerUserName = "Athlete";
    private string headerUserInitials = "U";
    private string headerUserRole = "Trainee";
    private string? headerUserAvatarUrl;
    private int headerCartCount = 0;
    private bool viewerIsCoach;
    private bool viewerIsTrainee = true;

    // Packages & Cart State
    private List<FitSocial.Client.Models.TrainingPackages.TrainingPackageResponseDto> coachPackages = new();
    private string? toastSuccessMessage;
    private Guid? addingPackageId;
    private string? addingPlaceholderKey;

    /// <summary>
    /// Governs whether the Message button is rendered in the UI.
    /// Trainee viewing another Trainee => Hidden (not rendered).
    /// Trainee viewing Coach, Coach viewing Trainee, Coach viewing Coach => Shown.
    /// </summary>
    private bool ShouldShowMessageButton =>
        profile != null && !(viewerIsTrainee && profile.IsTrainee);

    protected override async Task OnInitializedAsync()
    {
        await LoadHeaderUserInfoAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadProfileAsync();
    }

    private async Task LoadHeaderUserInfoAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                headerUserName = user.Identity.Name ?? "Athlete";
                var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
                viewerIsCoach = string.Equals(roleClaim, "COACH", StringComparison.OrdinalIgnoreCase);
                viewerIsTrainee = string.Equals(roleClaim, "TRAINEE", StringComparison.OrdinalIgnoreCase) || !viewerIsCoach;
                headerUserRole = viewerIsCoach ? "Coach" : "Trainee";
                headerUserInitials = AvatarHelper.GetInitials(headerUserName);
                headerUserAvatarUrl = user.FindFirst("avatar")?.Value ?? user.FindFirst("picture")?.Value;
            }
        }
        catch
        {
            headerUserInitials = "U";
            headerUserAvatarUrl = null;
        }

        await LoadCartCountAsync();
    }

    private async Task LoadCartCountAsync()
    {
        try
        {
            var res = await CartService.GetCartCountAsync();
            if (res.Success)
            {
                headerCartCount = res.Data;
            }
        }
        catch
        {
            // Ignore cart count errors
        }
    }

    private async Task LoadProfileAsync()
    {
        isLoading = true;
        errorMessage = null;
        isUnauthorized = false;
        profile = null;
        coachPackages.Clear();
        StateHasChanged();

        if (string.IsNullOrWhiteSpace(UserId) || !Guid.TryParse(UserId, out var targetUserId))
        {
            errorMessage = "User profile could not be found.";
            isLoading = false;
            StateHasChanged();
            return;
        }

        try
        {
            var response = await UserService.GetUserProfileAsync(targetUserId);
            if (response.Success && response.Data != null)
            {
                profile = response.Data;
                if (profile.IsCoach)
                {
                    await LoadCoachPackagesAsync(targetUserId);
                }
            }
            else
            {
                var msg = response.Message ?? string.Empty;
                if (msg.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    isUnauthorized = true;
                    errorMessage = "Your session has expired. Please sign in again.";
                }
                else if (!string.IsNullOrWhiteSpace(response.Message))
                {
                    errorMessage = response.Message;
                }
                else
                {
                    errorMessage = "User profile could not be found.";
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadCoachPackagesAsync(Guid coachId)
    {
        try
        {
            var res = await PackageService.GetAllPackagesAsync(coachId: coachId);
            if (res.Success && res.Data != null)
            {
                coachPackages = res.Data.ToList();
            }
        }
        catch
        {
            // Ignore package load errors
        }
    }

    /// <summary>
    /// Placeholder handler for Message button (UI only at this stage).
    /// Does not navigate, create conversations, or call messaging APIs.
    /// </summary>
    private void HandleMessageClick()
    {
        // UI only: Chat integration to be connected later
    }

    private void HandlePackageView(Guid packageId)
    {
        Navigation.NavigateTo($"/training-packages/{packageId}");
    }

    private async Task HandleAddToCart(Guid packageId, string? title)
    {
        addingPackageId = packageId;
        toastSuccessMessage = null;
        var displayTitle = !string.IsNullOrWhiteSpace(title) ? title : "Package";
        try
        {
            var res = await CartService.AddToCartAsync(new FitSocial.Client.Models.Cart.AddToCartRequestDto
            {
                PackageId = packageId,
                Quantity = 1
            });

            if (res.Success)
            {
                toastSuccessMessage = $"Added \"{displayTitle}\" to cart!";
                await LoadCartCountAsync();
                _ = Task.Delay(4000).ContinueWith(_ =>
                {
                    toastSuccessMessage = null;
                    InvokeAsync(StateHasChanged);
                });
            }
            else
            {
                toastSuccessMessage = res.Message ?? "Failed to add to cart.";
            }
        }
        catch (Exception ex)
        {
            toastSuccessMessage = ex.Message;
        }
        finally
        {
            addingPackageId = null;
            StateHasChanged();
        }
    }

    private async Task HandlePlaceholderAddToCart(string key, string title)
    {
        addingPlaceholderKey = key;
        toastSuccessMessage = null;
        try
        {
            var all = await PackageService.GetAllPackagesAsync();
            var firstPkg = all.Data?.FirstOrDefault();
            if (firstPkg != null)
            {
                var res = await CartService.AddToCartAsync(new FitSocial.Client.Models.Cart.AddToCartRequestDto
                {
                    PackageId = firstPkg.PackageId,
                    Quantity = 1
                });
                if (res.Success)
                {
                    toastSuccessMessage = $"Added \"{title}\" to cart!";
                    await LoadCartCountAsync();
                    _ = Task.Delay(4000).ContinueWith(_ =>
                    {
                        toastSuccessMessage = null;
                        InvokeAsync(StateHasChanged);
                    });
                    return;
                }
            }

            toastSuccessMessage = $"Added \"{title}\" to cart!";
            headerCartCount++;
            _ = Task.Delay(4000).ContinueWith(_ =>
            {
                toastSuccessMessage = null;
                InvokeAsync(StateHasChanged);
            });
        }
        catch (Exception ex)
        {
            toastSuccessMessage = ex.Message;
        }
        finally
        {
            addingPlaceholderKey = null;
            StateHasChanged();
        }
    }

    private async Task HandlePlaceholderView(string title)
    {
        try
        {
            var all = await PackageService.GetAllPackagesAsync();
            var firstPkg = all.Data?.FirstOrDefault();
            if (firstPkg != null)
            {
                Navigation.NavigateTo($"/training-packages/{firstPkg.PackageId}");
                return;
            }
        }
        catch
        {
            // Ignore
        }
        Navigation.NavigateTo("/training-packages");
    }

    private void HandlePackageClick()
    {
        Navigation.NavigateTo("/training-packages");
    }

    private void GoToHome()
    {
        Navigation.NavigateTo("home");
    }

    private void GoToLogin()
    {
        Navigation.NavigateTo("login");
    }

    private async Task HandleLogout()
    {
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("login");
    }

    private void HandleHeaderSearchInput(ChangeEventArgs e)
    {
        headerSearchTerm = e.Value?.ToString() ?? string.Empty;
    }

    private void HandleHeaderSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            ExecuteHeaderSearch();
        }
    }

    private void ExecuteHeaderSearch()
    {
        if (!string.IsNullOrWhiteSpace(headerSearchTerm))
        {
            Navigation.NavigateTo($"home?search={Uri.EscapeDataString(headerSearchTerm.Trim())}");
        }
    }

    private void ClearHeaderSearch()
    {
        headerSearchTerm = string.Empty;
    }
}
