using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Posts;
using FitSocial.Client.Models.TrainingPackages;
using FitSocial.Client.Models.Users;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Posts;
using FitSocial.Client.Services.TrainingPackages;
using FitSocial.Client.Services.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

namespace FitSocial.Client.Pages;

public partial class UserProfile : ComponentBase, IDisposable
{
    private enum ProfileTab
    {
        PersonalInfo,
        Posts,
        TrainingPackages
    }

    [Parameter] public string? UserId { get; set; }

    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private IPostService PostService { get; set; } = default!;
    [Inject] private ITrainingPackageService PackageService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private OtherUserProfileModel? profile;
    private bool isLoading = true;
    private string? errorMessage;
    private bool isUnauthorized;

    // Active tab (Default: PersonalInfo)
    private ProfileTab activeTab = ProfileTab.PersonalInfo;

    // Header & Viewer State (Current authenticated user)
    private Guid? currentUserId;
    private string headerSearchTerm = string.Empty;
    private string headerUserName = "Athlete";
    private string headerUserInitials = "U";
    private string headerUserRole = "Trainee";
    private string? headerUserAvatarUrl;
    private bool viewerIsCoach;
    private bool viewerIsTrainee = true;

    // Posts tab state
    private List<PostDto>? userPosts;
    private bool isLoadingPosts = false;
    private string? postsErrorMessage;
    private readonly HashSet<Guid> _pendingReactionPostIds = new();
    private readonly HashSet<Guid> reportedPostIds = new();
    private bool showReportModal = false;
    private Guid? reportPostId;

    // Training packages tab state
    private List<TrainingPackageResponseDto>? coachPackages;
    private bool isLoadingPackages = false;
    private string? packagesErrorMessage;

    // Toast notifications
    private string? toastMessage;
    private bool toastIsWarning;
    private CancellationTokenSource? toastCts;

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
                var idClaim = user.FindFirst("sub")?.Value
                              ?? user.FindFirst("nameid")?.Value
                              ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(idClaim, out var parsedId))
                {
                    currentUserId = parsedId;
                }

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

        // Reset tab state to default
        activeTab = ProfileTab.PersonalInfo;
        userPosts = null;
        coachPackages = null;
        isLoadingPosts = false;
        isLoadingPackages = false;
        postsErrorMessage = null;
        packagesErrorMessage = null;

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

    private async Task SwitchTab(ProfileTab tab)
    {
        if (tab == ProfileTab.TrainingPackages && (profile == null || !profile.IsCoach))
        {
            return;
        }

        activeTab = tab;

        if (activeTab == ProfileTab.Posts && userPosts == null && !isLoadingPosts)
        {
            await LoadUserPostsAsync();
        }
        else if (activeTab == ProfileTab.TrainingPackages && coachPackages == null && !isLoadingPackages && profile?.IsCoach == true)
        {
            await LoadCoachPackagesAsync();
        }
    }

    private async Task LoadUserPostsAsync()
    {
        if (profile == null || profile.UserId == Guid.Empty) return;

        isLoadingPosts = true;
        postsErrorMessage = null;
        StateHasChanged();

        try
        {
            var query = new GetPostsQuery
            {
                AuthorId = profile.UserId,
                PageNumber = 1,
                PageSize = 50
            };

            var response = await PostService.GetPostsAsync(query);
            if (response.Success && response.Data != null)
            {
                userPosts = response.Data.Items?.ToList() ?? new List<PostDto>();
            }
            else
            {
                postsErrorMessage = response.Message ?? "Unable to load published posts.";
                userPosts = new List<PostDto>();
            }
        }
        catch (Exception ex)
        {
            postsErrorMessage = $"Connection error: {ex.Message}";
            userPosts = new List<PostDto>();
        }
        finally
        {
            isLoadingPosts = false;
            StateHasChanged();
        }
    }

    private async Task LoadCoachPackagesAsync()
    {
        if (profile == null || !profile.IsCoach || profile.UserId == Guid.Empty) return;

        isLoadingPackages = true;
        packagesErrorMessage = null;
        StateHasChanged();

        try
        {
            var response = await PackageService.GetPackagesByCoachIdAsync(profile.UserId);
            if (response.Success && response.Data != null)
            {
                coachPackages = response.Data.ToList();
            }
            else
            {
                packagesErrorMessage = response.Message ?? "Unable to load training packages.";
                coachPackages = new List<TrainingPackageResponseDto>();
            }
        }
        catch (Exception ex)
        {
            packagesErrorMessage = $"Connection error: {ex.Message}";
            coachPackages = new List<TrainingPackageResponseDto>();
        }
        finally
        {
            isLoadingPackages = false;
            StateHasChanged();
        }
    }

    private async Task HandleLikePost(PostDto post)
    {
        if (_pendingReactionPostIds.Contains(post.Id)) return;
        _pendingReactionPostIds.Add(post.Id);

        var wasLiked = post.IsLikedByCurrentUser;
        var previousCount = post.LikeCount;

        post.IsLikedByCurrentUser = !wasLiked;
        post.LikeCount = wasLiked ? Math.Max(0, post.LikeCount - 1) : post.LikeCount + 1;
        StateHasChanged();

        try
        {
            var response = await PostService.ToggleLikeAsync(post.Id);
            if (response.Success && response.Data != null)
            {
                post.LikeCount = response.Data.LikeCount;
                post.IsLikedByCurrentUser = response.Data.IsLiked;
                StateHasChanged();
            }
            else
            {
                post.IsLikedByCurrentUser = wasLiked;
                post.LikeCount = previousCount;
                StateHasChanged();
            }
        }
        catch
        {
            post.IsLikedByCurrentUser = wasLiked;
            post.LikeCount = previousCount;
            StateHasChanged();
        }
        finally
        {
            _pendingReactionPostIds.Remove(post.Id);
        }
    }

    private async Task HandleReportPost(Guid postId)
    {
        if (reportedPostIds.Contains(postId))
        {
            ShowToast("You have already submitted a report for this post.", isWarning: true);
            return;
        }

        try
        {
            var checkRes = await PostService.CheckPostReportedAsync(postId);
            if (checkRes.Success && checkRes.Data)
            {
                reportedPostIds.Add(postId);
                ShowToast("You have already submitted a report for this post.", isWarning: true);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserProfile] Error checking report status for post {postId}: {ex.Message}");
        }

        reportPostId = postId;
        showReportModal = true;
    }

    private void CloseReportModal()
    {
        showReportModal = false;
        reportPostId = null;
    }

    private void OnPostReported(Guid reportedTargetPostId)
    {
        reportedPostIds.Add(reportedTargetPostId);
        CloseReportModal();
        ShowToast("Report submitted successfully. Thank you for your feedback.", isWarning: false);
    }

    private void NavigateToPackageDetails(Guid packageId)
    {
        Navigation.NavigateTo($"/training-packages/{packageId}");
    }

    private void ShowToast(string message, bool isWarning = false)
    {
        toastMessage = message;
        toastIsWarning = isWarning;
        StateHasChanged();

        toastCts?.Cancel();
        toastCts?.Dispose();
        toastCts = new CancellationTokenSource();
        var token = toastCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3500, token);
                if (!token.IsCancellationRequested)
                {
                    await InvokeAsync(() =>
                    {
                        toastMessage = null;
                        StateHasChanged();
                    });
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    /// <summary>
    /// Placeholder handler for Message button (UI only at this stage).
    /// Does not navigate, create conversations, or call messaging APIs.
    /// </summary>
    private void HandleMessageClick()
    {
        // UI only: Chat integration to be connected later
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

    public void Dispose()
    {
        toastCts?.Cancel();
        toastCts?.Dispose();
    }
}
