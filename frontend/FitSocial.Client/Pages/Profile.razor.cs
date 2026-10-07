using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Users;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Files;
using FitSocial.Client.Services.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace FitSocial.Client.Pages;

public partial class Profile : ComponentBase
{
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private IFileUploadService FileUploadService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private UserProfileModel? profile;
    private bool isLoading = true;
    private string? errorMessage;
    private bool isUnauthorized;

    // Edit Mode State (UC_05.1)
    private bool isEditing;
    private bool isSaving;
    private bool isUploadingAvatar;
    private bool isAvatarRemoved;
    private string? saveSuccessMessage;
    private string? saveErrorMessage;
    private string? avatarUploadError;
    private UpdateOwnProfileRequest editModel = new();
    private string? editDobString;

    // Field-level Validation Errors
    private string? fullNameError;
    private string? phoneError;
    private string? dobError;
    private string? bioError;
    private string? experienceError;

    // Header State
    private string headerSearchTerm = string.Empty;
    private string headerUserName = "Athlete";
    private string headerUserInitials = "U";
    private string headerUserRole = "Trainee";
    private string? headerUserAvatarUrl;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity?.IsAuthenticated == true && (user.IsInRole("STAFF") || user.IsInRole("ADMIN")))
        {
            Navigation.NavigateTo("/admin/profile", replace: true);
            return;
        }

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

    private void EnterEditMode()
    {
        if (profile == null) return;

        editModel = new UpdateOwnProfileRequest
        {
            FullName = profile.FullName ?? string.Empty,
            AvatarUrl = profile.AvatarUrl,
            DateOfBirth = profile.DateOfBirth,
            Gender = profile.Gender,
            PhoneNumber = profile.PhoneNumber,
            Bio = profile.Bio,
            ExperienceYears = profile.ExperienceYears
        };

        isAvatarRemoved = false;
        editDobString = profile.DateOfBirth?.ToString("yyyy-MM-dd");
        ClearErrors();
        saveSuccessMessage = null;
        saveErrorMessage = null;
        isEditing = true;
    }

    private void CancelEdit()
    {
        isEditing = false;
        isAvatarRemoved = false;
        ClearErrors();
        saveErrorMessage = null;
    }

    private void ClearErrors()
    {
        fullNameError = null;
        phoneError = null;
        dobError = null;
        bioError = null;
        experienceError = null;
        avatarUploadError = null;
    }

    private void OnDobChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        editDobString = val;
        if (string.IsNullOrWhiteSpace(val))
        {
            editModel.DateOfBirth = null;
            dobError = null;
        }
        else if (DateOnly.TryParse(val, out var parsed))
        {
            editModel.DateOfBirth = parsed;
            ValidateDob();
        }
        else
        {
            dobError = "Invalid date format.";
        }
    }

    private bool ValidateFullName()
    {
        var name = editModel.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            // Empty input retains existing name
            fullNameError = null;
            return true;
        }
        if (name.Length < 2 || name.Length > 100)
        {
            fullNameError = "Full name must be between 2 and 100 characters.";
            return false;
        }
        if (name.Any(char.IsDigit))
        {
            fullNameError = "Full name cannot contain numbers.";
            return false;
        }
        fullNameError = null;
        return true;
    }

    private bool ValidatePhone()
    {
        var phone = editModel.PhoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            // Empty input retains existing phone
            phoneError = null;
            return true;
        }
        if (!Regex.IsMatch(phone, @"^0\d{9}$"))
        {
            phoneError = "Phone number must be a valid 10-digit number starting with 0.";
            return false;
        }
        phoneError = null;
        return true;
    }

    private bool ValidateDob()
    {
        if (editModel.DateOfBirth.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (editModel.DateOfBirth.Value > today)
            {
                dobError = "Date of birth cannot be in the future.";
                return false;
            }
            if (editModel.DateOfBirth.Value.Year < 1900)
            {
                dobError = "Date of birth is invalid.";
                return false;
            }
        }
        dobError = null;
        return true;
    }

    private bool ValidateExperience()
    {
        if (profile?.IsCoach == true && editModel.ExperienceYears.HasValue)
        {
            if (editModel.ExperienceYears.Value < 0 || editModel.ExperienceYears.Value > 60)
            {
                experienceError = "Years of experience must be between 0 and 60.";
                return false;
            }
        }
        experienceError = null;
        return true;
    }

    private bool ValidateBio()
    {
        if (profile?.IsCoach == true && !string.IsNullOrWhiteSpace(editModel.Bio))
        {
            if (editModel.Bio.Trim().Length > 2000)
            {
                bioError = "Biography must not exceed 2000 characters.";
                return false;
            }
        }
        bioError = null;
        return true;
    }

    private bool ValidateForm()
    {
        var v1 = ValidateFullName();
        var v2 = ValidatePhone();
        var v3 = ValidateDob();
        var v4 = ValidateExperience();
        var v5 = ValidateBio();
        return v1 && v2 && v3 && v4 && v5;
    }

    private void OnFullNameBlur() => ValidateFullName();
    private void OnPhoneBlur() => ValidatePhone();
    private void OnExperienceBlur() => ValidateExperience();
    private void OnBioBlur() => ValidateBio();

    private async Task HandleAvatarSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        avatarUploadError = null;
        if (file.Size > 10 * 1024 * 1024)
        {
            avatarUploadError = "File size must not exceed 10MB.";
            return;
        }

        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
        {
            avatarUploadError = "Only JPG, PNG, or WEBP images are allowed.";
            return;
        }

        isUploadingAvatar = true;
        StateHasChanged();

        try
        {
            var res = await FileUploadService.UploadAvatarAsync(file);
            if (res.Success && !string.IsNullOrWhiteSpace(res.Data))
            {
                editModel.AvatarUrl = res.Data;
                isAvatarRemoved = false;
            }
            else
            {
                avatarUploadError = res.Message ?? "Failed to upload avatar image.";
            }
        }
        catch (Exception ex)
        {
            avatarUploadError = $"Upload error: {ex.Message}";
        }
        finally
        {
            isUploadingAvatar = false;
            StateHasChanged();
        }
    }

    private void RemoveAvatar()
    {
        editModel.AvatarUrl = null;
        isAvatarRemoved = true;
    }

    private async Task SaveProfileAsync()
    {
        saveErrorMessage = null;
        saveSuccessMessage = null;

        if (!ValidateForm())
        {
            saveErrorMessage = "Please correct the highlighted errors before saving.";
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            // Retain old values when input fields are left blank
            var effectiveFullName = string.IsNullOrWhiteSpace(editModel.FullName)
                ? (profile?.FullName ?? string.Empty)
                : editModel.FullName.Trim();

            var effectivePhone = string.IsNullOrWhiteSpace(editModel.PhoneNumber)
                ? profile?.PhoneNumber
                : editModel.PhoneNumber.Trim();

            var effectiveDob = editModel.DateOfBirth ?? profile?.DateOfBirth;

            var effectiveGender = string.IsNullOrWhiteSpace(editModel.Gender)
                ? profile?.Gender
                : editModel.Gender.Trim();

            var effectiveBio = string.IsNullOrWhiteSpace(editModel.Bio)
                ? profile?.Bio
                : editModel.Bio.Trim();

            var effectiveExperience = editModel.ExperienceYears ?? profile?.ExperienceYears;

            string? effectiveAvatar;
            if (isAvatarRemoved)
            {
                effectiveAvatar = "[REMOVE]";
            }
            else if (!string.IsNullOrWhiteSpace(editModel.AvatarUrl))
            {
                effectiveAvatar = editModel.AvatarUrl.Trim();
            }
            else
            {
                effectiveAvatar = profile?.AvatarUrl;
            }

            var payload = new UpdateOwnProfileRequest
            {
                FullName = effectiveFullName,
                AvatarUrl = effectiveAvatar,
                DateOfBirth = effectiveDob,
                Gender = effectiveGender,
                PhoneNumber = effectivePhone,
                Bio = effectiveBio,
                ExperienceYears = effectiveExperience
            };

            var response = await UserService.UpdateOwnProfileAsync(payload);
            if (response.Success && response.Data != null)
            {
                profile = response.Data;
                isAvatarRemoved = false;
                headerUserName = profile.FullName;
                headerUserInitials = AvatarHelper.GetInitials(profile.FullName);
                headerUserAvatarUrl = profile.AvatarUrl;
                isEditing = false;
                saveSuccessMessage = "Your profile has been updated successfully!";

                _ = Task.Run(async () =>
                {
                    await Task.Delay(4000);
                    saveSuccessMessage = null;
                    await InvokeAsync(StateHasChanged);
                });
            }
            else
            {
                saveErrorMessage = response.Message ?? "Failed to update profile.";
                if (response.Errors != null && response.Errors.Count > 0)
                {
                    saveErrorMessage += " " + string.Join(" ", response.Errors);
                }
            }
        }
        catch (Exception ex)
        {
            saveErrorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            isSaving = false;
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
