using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Posts;
using FitSocial.Client.Models.Locations;
using FitSocial.Client.Models.Coaches;

namespace FitSocial.Client.Pages;

public partial class Home : ComponentBase, IDisposable
{
    // Page state
    private List<PostDto>? posts;
    private bool isLoading = true;
    private bool showProfileMenu = false;
    private bool showCreatePostModal = false;
    private string? createPostInitialType;
    private string? toastSuccessMessage;
    private CancellationTokenSource? toastCts;

    // User state
    private Guid? currentUserId;
    private bool isCoach = false;
    private string userName = "Me";
    private string userInitials = "Me";
    private string userRole = "Athlete";
    private string? currentUserAvatarUrl;

    // Top coaches state
    private List<TopCoachDto> topCoaches = new();
    private bool isLoadingTopCoaches = true;

    // Edit post state
    private bool showEditPostModal = false;
    private PostDto? editingPost;

    // Delete post state
    private bool showDeleteModal = false;
    private PostDto? postToDelete;

    // Report post state
    private bool showReportModal = false;
    private Guid? reportPostId;
    private readonly HashSet<Guid> reportedPostIds = new();
    private string? toastWarningMessage;
    private CancellationTokenSource? warningToastCts;

    // Dropdowns data
    private List<LocationDto> availableLocations = new();

    // Query & Filter state
    private string searchTerm = string.Empty;
    private CancellationTokenSource? searchCts;

    private string? selectedPostType;
    private Guid? selectedFilterLocationId;

    private bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(selectedPostType) ||
        (selectedFilterLocationId.HasValue && selectedFilterLocationId != Guid.Empty);

    private int currentPage = 1;
    private int pageSize = 10;
    private int totalCount = 0;
    private bool hasNextPage = false;
    private bool isLoadingMore = false;

    private GetPostsQuery currentQuery = new()
    {
        PageNumber = 1,
        PageSize = 10
    };

    // Lifecycle
    protected override async Task OnInitializedAsync()
    {
        await LoadUserInfoAsync();
        await Task.WhenAll(LoadPostsAsync(), LoadLocationsAsync(), LoadTopCoachesAsync());
    }

    private async Task LoadUserInfoAsync()
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

                userName = user.Identity.Name ?? "Athlete";
                isCoach = user.IsInRole(AppRoles.Coach) ||
                          user.IsInRole("COACH") ||
                          string.Equals(user.FindFirst(ClaimTypes.Role)?.Value, AppRoles.Coach, StringComparison.OrdinalIgnoreCase);
                userRole = isCoach ? "Coach" : "Trainee";
                userInitials = AvatarHelper.GetInitials(userName);

                var meRes = await AuthService.GetCurrentUserAsync();
                if (meRes.Success && meRes.Data != null)
                {
                    currentUserAvatarUrl = meRes.Data.AvatarUrl;
                    if (!string.IsNullOrWhiteSpace(meRes.Data.FullName))
                    {
                        userName = meRes.Data.FullName;
                        userInitials = AvatarHelper.GetInitials(userName);
                    }
                    StateHasChanged();
                }
                else
                {
                    Console.WriteLine($"[Home] AuthService.GetCurrentUserAsync returned: Success={meRes.Success}, Message='{meRes.Message}'");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Home] Error loading user info: {ex.Message}");
            userInitials = "Me";
        }
        finally
        {
            StateHasChanged();
        }
    }

    private async Task LoadTopCoachesAsync()
    {
        try
        {
            isLoadingTopCoaches = true;
            var response = await CoachService.GetTopCoachesAsync(5);
            if (response.Success && response.Data != null)
            {
                topCoaches = response.Data;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading top coaches: {ex.Message}");
        }
        finally
        {
            isLoadingTopCoaches = false;
        }
    }

    private async Task LoadLocationsAsync()
    {
        try
        {
            var locationsResponse = await LocationService.GetLocationsAsync();
            if (locationsResponse.Success && locationsResponse.Data != null)
            {
                availableLocations = locationsResponse.Data;
            }
        }
        catch
        {
            // Graceful fallback
        }
    }
    private async Task LoadPostsAsync(bool append = false)
    {
        if (!append)
        {
            isLoading = true;
            currentPage = 1;
        }
        else
        {
            isLoadingMore = true;
        }

        currentQuery.PageNumber = currentPage;
        currentQuery.PageSize = pageSize;
        currentQuery.SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        currentQuery.PostType = string.IsNullOrWhiteSpace(selectedPostType) ? null : selectedPostType;
        currentQuery.LocationId = selectedFilterLocationId.HasValue && selectedFilterLocationId != Guid.Empty ? selectedFilterLocationId : null;

        try
        {
            var response = await PostService.GetPostsAsync(currentQuery);
            if (response.Success && response.Data != null)
            {
                var incoming = response.Data.Items ?? new List<PostDto>();
                if (append && posts != null)
                {
                    var existingIds = new HashSet<Guid>(posts.Select(p => p.Id));
                    var unique = incoming.Where(p => !existingIds.Contains(p.Id)).ToList();
                    posts.AddRange(unique);
                }
                else
                {
                    posts = incoming;
                }

                totalCount = response.Data.TotalCount;
                hasNextPage = response.Data.HasNextPage;
            }
            else
            {
                if (!append)
                {
                    posts = new List<PostDto>();
                }
                hasNextPage = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Home] Error loading posts: {ex.Message}");
            if (!append)
            {
                posts = new List<PostDto>();
            }
            hasNextPage = false;
        }
        finally
        {
            isLoading = false;
            isLoadingMore = false;
            StateHasChanged();
        }
    }

    private async Task LoadMorePostsAsync()
    {
        if (isLoadingMore || !hasNextPage) return;
        currentPage++;
        await LoadPostsAsync(append: true);
    }

    // Modal Coordination
    private void HandleCreatePostBoxOpen((string? trigger, string? postType) args)
    {
        OpenCreatePostModal(args.trigger, args.postType);
    }

    private void OpenCreatePostModal(string? trigger = null, string? postType = null)
    {
        createPostInitialType = postType;
        showCreatePostModal = true;
    }

    private void CloseCreatePostModal()
    {
        showCreatePostModal = false;
        createPostInitialType = null;
    }

    private void OnPostCreated(PostDto newPost)
    {
        posts?.Insert(0, newPost);
        totalCount++;
        showCreatePostModal = false;
        createPostInitialType = null;
        ShowSuccessToast("Post published successfully!");
    }

    // Search Handlers
    private void HandleSearchInput(ChangeEventArgs e)
    {
        searchTerm = e.Value?.ToString() ?? string.Empty;
        searchCts?.Cancel();
        searchCts?.Dispose();
        searchCts = new CancellationTokenSource();
        var token = searchCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);
                if (!token.IsCancellationRequested)
                {
                    await InvokeAsync(async () =>
                    {
                        currentPage = 1;
                        await LoadPostsAsync();
                    });
                }
            }
            catch (TaskCanceledException)
            {
                // Debounce cancelled by new keystroke
            }
        });
    }

    private async Task HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            searchCts?.Cancel();
            await ExecuteSearch();
        }
    }

    private async Task ExecuteSearch()
    {
        searchCts?.Cancel();
        currentPage = 1;
        await LoadPostsAsync();
    }

    private async Task ClearSearch()
    {
        searchCts?.Cancel();
        searchTerm = string.Empty;
        currentPage = 1;
        await LoadPostsAsync();
    }

    // Filter Handlers
    private async Task HandleApplyFilters((string? postType, Guid? locationId) filters)
    {
        selectedPostType = filters.postType;
        selectedFilterLocationId = filters.locationId;
        currentPage = 1;
        await LoadPostsAsync();
    }
    private async Task ResetAllSearchAndFilters()
    {
        searchCts?.Cancel();
        searchTerm = string.Empty;
        selectedPostType = null;
        selectedFilterLocationId = null;
        currentPage = 1;
        await LoadPostsAsync();
    }

    // Post Interactions
    private void ToggleLike(PostDto post)
    {
        post.IsLikedByCurrentUser = !post.IsLikedByCurrentUser;
        post.LikeCount += post.IsLikedByCurrentUser ? 1 : -1;
        if (post.LikeCount < 0) post.LikeCount = 0;

        _ = PostService.ToggleLikeAsync(post.Id);
    }

    private void ToggleProfileMenu()
    {
        showProfileMenu = !showProfileMenu;
    }

    private async Task HandleLogout()
    {
        showProfileMenu = false;
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("login");
    }

    private async Task HandleReportPost(Guid postId)
    {
        // 1. Check client-side cache for this specific post
        if (reportedPostIds.Contains(postId))
        {
            ShowWarningToast("You have already submitted a pending report for this post.");
            return;
        }

        // 2. Query backend duplicate check scoped strictly to (postId, reporterId)
        try
        {
            var checkRes = await PostService.CheckPostReportedAsync(postId);
            if (checkRes.Success && checkRes.Data)
            {
                reportedPostIds.Add(postId);
                ShowWarningToast("You have already submitted a pending report for this post.");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Home] Error checking report status for post {postId}: {ex.Message}");
        }

        // 3. Not reported yet: display the report form modal
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
        ShowSuccessToast("Report submitted successfully. Thank you for helping keep our community safe.");
    }

    // Delete post methods
    private void OpenDeletePostModal(PostDto post)
    {
        postToDelete = post;
        showDeleteModal = true;
    }

    private void CloseDeleteModal()
    {
        showDeleteModal = false;
        postToDelete = null;
    }

    private void OnPostDeleted(Guid deletedPostId)
    {
        posts?.RemoveAll(p => p.Id == deletedPostId);
        showDeleteModal = false;
        postToDelete = null;
        ShowSuccessToast("Post deleted successfully.");
        StateHasChanged();
    }

    // Edit post methods
    private void OpenEditPostModal(PostDto post)
    {
        editingPost = post;
        showEditPostModal = true;
    }

    private void CloseEditPostModal()
    {
        showEditPostModal = false;
        editingPost = null;
    }

    private void OnPostUpdated(PostDto updatedPost)
    {
        var targetEditId = updatedPost.Id;
        var index = posts?.FindIndex(p => p.Id == targetEditId) ?? -1;
        if (index >= 0 && posts != null)
        {
            updatedPost.IsLikedByCurrentUser = posts[index].IsLikedByCurrentUser;
            posts[index] = updatedPost;
            posts = new List<PostDto>(posts);
        }

        showEditPostModal = false;
        editingPost = null;
        ShowSuccessToast("Post updated successfully!");
        StateHasChanged();
    }

    // Toast Notifications
    private void ShowSuccessToast(string message)
    {
        toastCts?.Cancel();
        toastCts?.Dispose();
        toastCts = new CancellationTokenSource();
        toastSuccessMessage = message;
        StateHasChanged();

        var token = toastCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3500, token);
                if (!token.IsCancellationRequested)
                {
                    toastSuccessMessage = null;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignored
            }
        });
    }

    private void DismissToast()
    {
        toastCts?.Cancel();
        toastSuccessMessage = null;
        StateHasChanged();
    }

    private void ShowWarningToast(string message)
    {
        warningToastCts?.Cancel();
        warningToastCts?.Dispose();
        warningToastCts = new CancellationTokenSource();
        toastWarningMessage = message;
        StateHasChanged();

        var token = warningToastCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3500, token);
                if (!token.IsCancellationRequested)
                {
                    toastWarningMessage = null;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignored
            }
        });
    }

    private void DismissWarningToast()
    {
        warningToastCts?.Cancel();
        toastWarningMessage = null;
        StateHasChanged();
    }

    // Dispose
    public void Dispose()
    {
        searchCts?.Cancel();
        searchCts?.Dispose();
        toastCts?.Cancel();
        toastCts?.Dispose();
        warningToastCts?.Cancel();
        warningToastCts?.Dispose();
    }
}
