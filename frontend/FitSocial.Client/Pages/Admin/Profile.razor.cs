using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Client.Models.Users;
using FitSocial.Client.Services.Files;
using FitSocial.Client.Services.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FitSocial.Client.Pages.Admin;

public partial class Profile : ComponentBase, IDisposable
{
    private StaffAdminOwnProfileDto? profile;
    private bool isLoading = true;
    private string? errorMessage;
    private string? successMessage;
    private string? saveErrorMessage;

    private CancellationTokenSource? successMessageCts;
    private CancellationTokenSource? saveErrorCts;

    // Edit mode state
    private bool isEditing = false;
    private bool isSaving = false;
    private bool isUploadingAvatar = false;
    private string? avatarUploadError;
    private bool isAvatarRemoved = false;
    private string? newUploadedAvatarUrl;

    // Form inputs
    private string? inputFullName;
    private string? inputPhoneNumber;
    private string? inputDobString;
    private string? inputGender;

    // Validation errors
    private string? fullNameError;
    private string? phoneError;
    private string? dobError;
    private string? genderError;

    private static readonly Regex PhoneRegex = new(@"^0\d{9}$", RegexOptions.Compiled);

    private string? EffectiveAvatarUrl =>
        isAvatarRemoved ? null : (!string.IsNullOrWhiteSpace(newUploadedAvatarUrl) ? newUploadedAvatarUrl : profile?.Avatar);

    private bool HasValidationErrors =>
        !string.IsNullOrEmpty(fullNameError) ||
        !string.IsNullOrEmpty(phoneError) ||
        !string.IsNullOrEmpty(dobError) ||
        !string.IsNullOrEmpty(genderError);

    protected override async Task OnInitializedAsync()
    {
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        isLoading = true;
        errorMessage = null;
        StateHasChanged();

        try
        {
            var response = await UserService.GetStaffAdminOwnProfileAsync();
            if (response.Success && response.Data != null)
            {
                profile = response.Data;
            }
            else
            {
                errorMessage = !string.IsNullOrWhiteSpace(response.Message)
                    ? response.Message
                    : "Unable to load your profile. Please try again later.";
            }
        }
        catch (Exception)
        {
            errorMessage = "Unable to load your profile. Please try again later.";
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

        isEditing = true;
        DismissSuccessMessage();
        DismissSaveErrorMessage();
        avatarUploadError = null;
        isAvatarRemoved = false;
        newUploadedAvatarUrl = null;

        inputFullName = profile.FullName;
        inputPhoneNumber = profile.PhoneNumber;
        inputDobString = profile.DateOfBirth?.ToString("yyyy-MM-dd");
        inputGender = profile.Gender;

        ClearValidationErrors();
    }

    private void CancelEdit()
    {
        isEditing = false;
        DismissSaveErrorMessage();
        avatarUploadError = null;
        isAvatarRemoved = false;
        newUploadedAvatarUrl = null;
        ClearValidationErrors();
    }

    private void ClearValidationErrors()
    {
        fullNameError = null;
        phoneError = null;
        dobError = null;
        genderError = null;
    }

    private void ShowSuccessMessage(string message, int durationMs = 10000)
    {
        successMessage = message;
        saveErrorMessage = null;
        successMessageCts?.Cancel();
        successMessageCts?.Dispose();
        successMessageCts = new CancellationTokenSource();
        var token = successMessageCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(durationMs, token);
                if (!token.IsCancellationRequested)
                {
                    await InvokeAsync(() =>
                    {
                        successMessage = null;
                        StateHasChanged();
                    });
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    private void ShowSaveErrorMessage(string message, int durationMs = 10000)
    {
        saveErrorMessage = message;
        saveErrorCts?.Cancel();
        saveErrorCts?.Dispose();
        saveErrorCts = new CancellationTokenSource();
        var token = saveErrorCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(durationMs, token);
                if (!token.IsCancellationRequested)
                {
                    await InvokeAsync(() =>
                    {
                        saveErrorMessage = null;
                        StateHasChanged();
                    });
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    private void DismissSuccessMessage()
    {
        successMessageCts?.Cancel();
        successMessage = null;
    }

    private void DismissSaveErrorMessage()
    {
        saveErrorCts?.Cancel();
        saveErrorMessage = null;
    }

    private void ValidateFullName()
    {
        if (string.IsNullOrWhiteSpace(inputFullName))
        {
            // Empty / blank retains old value in database without validation error
            fullNameError = null;
            return;
        }

        var trimmed = inputFullName.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 100)
        {
            fullNameError = "Full name must be between 2 and 100 characters.";
            return;
        }

        if (trimmed.Any(char.IsDigit))
        {
            fullNameError = "Full name cannot contain numbers.";
            return;
        }

        fullNameError = null;
    }

    private void ValidatePhone()
    {
        if (string.IsNullOrWhiteSpace(inputPhoneNumber))
        {
            // Empty / blank retains old value in database without validation error
            phoneError = null;
            return;
        }

        var trimmed = inputPhoneNumber.Trim();
        if (!PhoneRegex.IsMatch(trimmed))
        {
            phoneError = "Phone number must be a valid 10-digit number starting with 0.";
            return;
        }

        phoneError = null;
    }

    private void OnDobChanged(ChangeEventArgs e)
    {
        inputDobString = e.Value?.ToString();
        ValidateDob();
    }

    private void ValidateDob()
    {
        if (string.IsNullOrWhiteSpace(inputDobString))
        {
            // Empty / blank retains old value in database without validation error
            dobError = null;
            return;
        }

        if (!DateOnly.TryParse(inputDobString, out var parsedDob))
        {
            dobError = "Invalid date format.";
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (parsedDob > today)
        {
            dobError = "Date of birth cannot be in the future.";
            return;
        }

        if (parsedDob.Year < 1900)
        {
            dobError = "Date of birth is invalid (must be after 1900).";
            return;
        }

        dobError = null;
    }

    private void ValidateGender()
    {
        if (string.IsNullOrWhiteSpace(inputGender))
        {
            // Empty / blank retains old value in database without validation error
            genderError = null;
            return;
        }

        var g = inputGender.Trim();
        if (!string.Equals(g, "Male", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(g, "Female", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(g, "Other", StringComparison.OrdinalIgnoreCase))
        {
            genderError = "Gender must be Male, Female, or Other.";
            return;
        }

        genderError = null;
    }

    private bool ValidateAll()
    {
        ValidateFullName();
        ValidatePhone();
        ValidateDob();
        ValidateGender();
        return !HasValidationErrors;
    }

    private async Task HandleAvatarSelected(InputFileChangeEventArgs e)
    {
        avatarUploadError = null;
        var file = e.File;
        if (file == null) return;

        var extension = Path.GetExtension(file.Name)?.ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
        {
            avatarUploadError = "Only image files (.jpg, .jpeg, .png, .webp) are allowed.";
            return;
        }

        if (file.Size > 5 * 1024 * 1024)
        {
            avatarUploadError = "Avatar file size must not exceed 5MB.";
            return;
        }

        isUploadingAvatar = true;
        StateHasChanged();

        try
        {
            var uploadResult = await FileUploadService.UploadAvatarAsync(file);
            if (uploadResult.Success && !string.IsNullOrWhiteSpace(uploadResult.Data))
            {
                newUploadedAvatarUrl = uploadResult.Data;
                isAvatarRemoved = false;
            }
            else
            {
                avatarUploadError = !string.IsNullOrWhiteSpace(uploadResult.Message)
                    ? uploadResult.Message
                    : "Failed to upload avatar image.";
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
        isAvatarRemoved = true;
        newUploadedAvatarUrl = null;
        avatarUploadError = null;
    }

    private void RestoreAvatar()
    {
        isAvatarRemoved = false;
        newUploadedAvatarUrl = null;
        avatarUploadError = null;
    }

    private async Task SaveProfileAsync()
    {
        if (profile == null || isSaving) return;

        saveErrorMessage = null;
        successMessage = null;

        if (!ValidateAll())
        {
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateStaffAdminProfileRequest();

            // Rule:
            // Field left blank / whitespace -> do not send (null) -> backend retains existing value in database.
            // Field entered with valid data -> send trimmed value -> backend updates.
            if (!string.IsNullOrWhiteSpace(inputFullName))
            {
                request.FullName = inputFullName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(inputPhoneNumber))
            {
                request.PhoneNumber = inputPhoneNumber.Trim();
            }

            if (!string.IsNullOrWhiteSpace(inputDobString) && DateOnly.TryParse(inputDobString, out var parsedDob))
            {
                request.DateOfBirth = parsedDob;
            }

            if (!string.IsNullOrWhiteSpace(inputGender))
            {
                request.Gender = inputGender.Trim();
            }

            if (isAvatarRemoved)
            {
                request.Avatar = "[REMOVE]";
                request.AvatarUrl = "[REMOVE]";
            }
            else if (!string.IsNullOrWhiteSpace(newUploadedAvatarUrl))
            {
                request.Avatar = newUploadedAvatarUrl;
                request.AvatarUrl = newUploadedAvatarUrl;
            }

            var response = await UserService.UpdateStaffAdminOwnProfileAsync(request);
            if (response.Success && response.Data != null)
            {
                profile = response.Data;
                isEditing = false;
                ShowSuccessMessage(!string.IsNullOrWhiteSpace(response.Message)
                    ? response.Message
                    : "Profile updated successfully!", 10000);
            }
            else
            {
                ShowSaveErrorMessage(!string.IsNullOrWhiteSpace(response.Message)
                    ? response.Message
                    : "Unable to update profile. Please try again.", 10000);
            }
        }
        catch (Exception)
        {
            ShowSaveErrorMessage("An unexpected error occurred while saving. Please try again later.", 10000);
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        successMessageCts?.Cancel();
        successMessageCts?.Dispose();
        saveErrorCts?.Cancel();
        saveErrorCts?.Dispose();
    }
}
