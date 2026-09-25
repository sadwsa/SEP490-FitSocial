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

public partial class Profile : ComponentBase
{
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private UserProfileModel? profile;
    private bool isLoading = true;
    private string? errorMessage;
    private bool isUnauthorized;

    // Header State
    private string headerSearchTerm = string.Empty;
    private string headerUserName = "Athlete";
    private string headerUserInitials = "U";
    private string headerUserRole = "Trainee";
    private string? headerUserAvatarUrl;

    protected override async Task OnInitializedAsync()
    {
        await LoadHeaderUserInfoAsync();
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
                var isCoach = string.Equals(roleClaim, "COACH", StringComparison.OrdinalIgnoreCase);
                headerUserRole = isCoach ? "Coach" : "Trainee";
                headerUserInitials = AvatarHelper.GetInitials(headerUserName);
            }
        }
        catch
        {
            headerUserInitials = "U";
        }
    }

    private async Task LoadProfileAsync()
    {
        isLoading = true;
        errorMessage = null;
        isUnauthorized = false;
        StateHasChanged();

        try
        {
            var response = await UserService.GetOwnProfileAsync();
            if (response.Success && response.Data != null)
            {
                profile = response.Data;

                // Sync header state with loaded profile data
                if (!string.IsNullOrWhiteSpace(profile.FullName))
                {
                    headerUserName = profile.FullName;
                    headerUserInitials = AvatarHelper.GetInitials(profile.FullName);
                }

                headerUserRole = profile.IsCoach ? "Coach" : "Trainee";
                headerUserAvatarUrl = profile.AvatarUrl;
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

    private static string GetApprovalBadgeClass(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "bg-amber-50 text-amber-700 border border-amber-200";
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "bg-emerald-50 text-emerald-700 border border-emerald-200",
            "REJECTED" => "bg-red-50 text-red-700 border border-red-200",
            _ => "bg-amber-50 text-amber-700 border border-amber-200"
        };
    }

    private static string GetApprovalStatusLabel(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Pending Verification";
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "Verified Coach",
            "REJECTED" => "Rejected",
            "PENDING" => "Pending Verification",
            _ => status
        };
    }
}
