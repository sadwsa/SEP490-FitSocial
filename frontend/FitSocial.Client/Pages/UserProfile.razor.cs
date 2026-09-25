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
    private bool viewerIsCoach;
    private bool viewerIsTrainee = true;

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
    }

    private async Task LoadProfileAsync()
    {
        isLoading = true;
        errorMessage = null;
        isUnauthorized = false;
        profile = null;
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

    /// <summary>
    /// Placeholder handler for Message button (UI only at this stage).
    /// Does not navigate, create conversations, or call messaging APIs.
    /// </summary>
    private void HandleMessageClick()
    {
        // UI only: Chat integration to be connected later
    }

    /// <summary>
    /// Placeholder handler for Package action (UI only at this stage).
    /// Does not navigate or trigger checkout/payment.
    /// </summary>
    private void HandlePackageClick()
    {
        // UI only: Package booking/purchasing to be connected later
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
