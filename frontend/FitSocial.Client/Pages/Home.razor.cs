using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Posts;
using FitSocial.Client.Models.Locations;
using FitSocial.Client.Models.Coaches;
using FitSocial.Client.Components.Posts;

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

    // Top coaches state -> chuyển sang widget mặc định của cột phải (Layout/RightSidebar.razor)

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

    // URL Query Parameters
    [SupplyParameterFromQuery(Name = "search")]
    public string? UrlSearch { get; set; }

    [SupplyParameterFromQuery(Name = "searchTerm")]
    public string? UrlSearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "postType")]
    public string? UrlPostType { get; set; }

    [SupplyParameterFromQuery(Name = "locationId")]
    public Guid? UrlLocationId { get; set; }

    private string? _lastAppliedUrlSearch;
    private string? _lastAppliedUrlPostType;
    private Guid? _lastAppliedUrlLocationId;
    private bool _hasInitialized;
    private long _loadPostsRequestId = 0;

    // Query & Filter state
    private string searchInput = string.Empty;
    private string? appliedSearchTerm;

    private string? draftPostType;
    private string? appliedPostType;

    private Guid? draftLocationId;
    private Guid? appliedLocationId;

    private bool HasSearchInput => !string.IsNullOrWhiteSpace(searchInput);
    private bool HasDraftFilter => !string.IsNullOrWhiteSpace(draftPostType) || (draftLocationId.HasValue && draftLocationId != Guid.Empty);
    private bool HasAppliedFilter => !string.IsNullOrWhiteSpace(appliedPostType) || (appliedLocationId.HasValue && appliedLocationId != Guid.Empty);
    private bool HasAppliedSearch => !string.IsNullOrWhiteSpace(appliedSearchTerm);

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
        PostHubClient.OnPostDeleted -= HandlePostDeletedRealtime;
        PostHubClient.OnPostDeleted += HandlePostDeletedRealtime;

        ApplyUrlQueryParameters();

        // Topbar do AppShellLayout render: đăng ký hành vi tìm kiếm cho trang này
        Shell.SearchInputHandler = e =>
        {
            HandleSearchInput(e);
            return Task.CompletedTask;
        };
        Shell.SearchKeyUpHandler = HandleSearchKeyUp;
        Shell.SearchExecuteHandler = ExecuteSearch;
        Shell.SearchClearHandler = ClearSearch;
        Shell.LogoClickHandler = HandleLogoClick;
        Shell.SearchTerm = searchInput;
        UpdateHeaderSearchExtra();

        await LoadUserInfoAsync();
        await Task.WhenAll(LoadPostsAsync(), LoadLocationsAsync(), PostHubClient.StartAsync());
        _hasInitialized = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_hasInitialized) return;

        var effectiveUrlSearch = !string.IsNullOrWhiteSpace(UrlSearch) ? UrlSearch : UrlSearchTerm;
        if (effectiveUrlSearch != _lastAppliedUrlSearch ||
            UrlPostType != _lastAppliedUrlPostType ||
            UrlLocationId != _lastAppliedUrlLocationId)
        {
            ApplyUrlQueryParameters();
            Shell.SearchTerm = searchInput;
            UpdateHeaderSearchExtra();
            currentPage = 1;
            await LoadPostsAsync();
        }
    }

    private void ApplyUrlQueryParameters()
    {
        var effectiveUrlSearch = !string.IsNullOrWhiteSpace(UrlSearch) ? UrlSearch : UrlSearchTerm;
        if (!string.IsNullOrWhiteSpace(effectiveUrlSearch))
        {
            searchInput = effectiveUrlSearch.Trim();
            appliedSearchTerm = searchInput;
        }

        if (!string.IsNullOrWhiteSpace(UrlPostType))
        {
            appliedPostType = NormalizePostType(UrlPostType);
            draftPostType = appliedPostType;
        }

        if (UrlLocationId.HasValue && UrlLocationId.Value != Guid.Empty)
        {
            appliedLocationId = UrlLocationId.Value;
            draftLocationId = appliedLocationId;
        }

        _lastAppliedUrlSearch = effectiveUrlSearch;
        _lastAppliedUrlPostType = UrlPostType;
        _lastAppliedUrlLocationId = UrlLocationId;
    }

    private static string? NormalizePostType(string? postType)
    {
        if (string.IsNullOrWhiteSpace(postType)) return null;
        var clean = postType.Trim().Replace(" ", "").ToLowerInvariant();
        return clean switch
        {
            "findcoach" => "Find Coach",
            "findtrainee" => "Find Trainee",
            "normal" => "Normal",
            _ => postType.Trim()
        };
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

    private async Task LoadLocationsAsync()
    {
        try
        {
            var locationsResponse = await LocationService.GetLocationsAsync();
            if (locationsResponse.Success && locationsResponse.Data != null)
            {
                availableLocations = locationsResponse.Data;
                UpdateHeaderSearchExtra();
            }
        }
        catch
        {
            // Graceful fallback
        }
    }
    private async Task LoadPostsAsync(bool append = false)
    {
        var requestId = Interlocked.Increment(ref _loadPostsRequestId);

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
        currentQuery.SearchTerm = string.IsNullOrWhiteSpace(appliedSearchTerm) ? null : appliedSearchTerm.Trim();
        currentQuery.PostType = string.IsNullOrWhiteSpace(appliedPostType) ? null : appliedPostType;
        currentQuery.LocationId = appliedLocationId.HasValue && appliedLocationId != Guid.Empty ? appliedLocationId : null;

        try
        {
            var response = await PostService.GetPostsAsync(currentQuery);
            if (requestId != _loadPostsRequestId)
            {
                // Discard stale response from superseded request
                return;
            }

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
            if (requestId != _loadPostsRequestId)
            {
                return;
            }

            Console.WriteLine($"[Home] Error loading posts: {ex.Message}");
            if (!append)
            {
                posts = new List<PostDto>();
            }
            hasNextPage = false;
        }
        finally
        {
            if (requestId == _loadPostsRequestId)
            {
                isLoading = false;
                isLoadingMore = false;
                StateHasChanged();
            }
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
        searchInput = e.Value?.ToString() ?? string.Empty;
        Shell.SearchTerm = searchInput;
        // Do NOT trigger any search/API call when user types or edits!
    }

    private async Task HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await ExecuteSearch();
        }
    }

    private async Task ExecuteSearch()
    {
        if (HasSearchInput)
        {
            appliedSearchTerm = searchInput.Trim();
            // Case 3: HasSearchInput = true, HasFilter = true -> Search + Filter
            if (HasDraftFilter)
            {
                appliedPostType = draftPostType;
                appliedLocationId = draftLocationId;
            }
            else if (!HasAppliedFilter)
            {
                appliedPostType = null;
                appliedLocationId = null;
            }
        }
        else
        {
            // Case 2 & 10: HasSearchInput = false
            appliedSearchTerm = null;
            // Rule: "Không được vô tình áp dụng filter chưa xác nhận"
            if (!HasAppliedFilter)
            {
                appliedPostType = null;
                appliedLocationId = null;
                draftPostType = null;
                draftLocationId = null;
            }
        }

        currentPage = 1;
        UpdateHeaderSearchExtra();
        await LoadPostsAsync();
    }

    private async Task ClearSearch()
    {
        searchInput = string.Empty;
        Shell.SearchTerm = string.Empty;
        if (appliedSearchTerm != null)
        {
            appliedSearchTerm = null;
            currentPage = 1;
            UpdateHeaderSearchExtra();
            await LoadPostsAsync();
        }
        else
        {
            UpdateHeaderSearchExtra();
        }
    }

    // Filter Handlers
    private void HandleDraftFilterChanged((string? postType, Guid? locationId) draft)
    {
        draftPostType = draft.postType;
        draftLocationId = draft.locationId;
        // Do NOT reload posts when user only changes selection in filter dropdown!
    }

    private async Task HandleApplyFilters((string? postType, Guid? locationId) filters)
    {
        draftPostType = filters.postType;
        draftLocationId = filters.locationId;
        appliedPostType = filters.postType;
        appliedLocationId = filters.locationId;

        if (HasSearchInput)
        {
            // Case 3 & 7: Both Search Input and Filter: Apply executes Search + Filter
            appliedSearchTerm = searchInput.Trim();
        }
        else
        {
            // Case 2 & 8: Filter only, Search Input was cleared or empty
            appliedSearchTerm = null;
        }

        currentPage = 1;
        UpdateHeaderSearchExtra();
        await LoadPostsAsync();
    }

    private async Task HandleLogoClick()
    {
        searchInput = string.Empty;
        Shell.SearchTerm = string.Empty;
        appliedSearchTerm = null;
        draftPostType = null;
        appliedPostType = null;
        draftLocationId = null;
        appliedLocationId = null;
        _lastAppliedUrlSearch = null;
        _lastAppliedUrlPostType = null;
        _lastAppliedUrlLocationId = null;
        currentPage = 1;
        Navigation.NavigateTo("home", replace: true);
        UpdateHeaderSearchExtra();
        await LoadPostsAsync();
    }

    private void UpdateHeaderSearchExtra()
    {
        Shell.HeaderSearchExtra = builder =>
        {
            builder.OpenComponent<PostFilterBar>(0);
            builder.AddAttribute(1, nameof(PostFilterBar.SelectedPostType), appliedPostType);
            builder.AddAttribute(2, nameof(PostFilterBar.SelectedLocationId), appliedLocationId);
            builder.AddAttribute(3, nameof(PostFilterBar.AvailableLocations), availableLocations);
            builder.AddAttribute(4, nameof(PostFilterBar.OnDraftFilterChanged), EventCallback.Factory.Create<(string? PostType, Guid? LocationId)>(this, HandleDraftFilterChanged));
            builder.AddAttribute(5, nameof(PostFilterBar.OnApplyFilters), EventCallback.Factory.Create<(string? PostType, Guid? LocationId)>(this, HandleApplyFilters));
            builder.CloseComponent();
        };
        Shell.NotifyChanged();
    }

    private async Task RemoveAppliedSearch()
    {
        searchInput = string.Empty;
        Shell.SearchTerm = string.Empty;
        appliedSearchTerm = null;
        currentPage = 1;
        UpdateHeaderSearchExtra();
        await LoadPostsAsync();
    }

    private async Task RemoveFilterPostType()
    {
        appliedPostType = null;
        draftPostType = null;
        currentPage = 1;
        UpdateHeaderSearchExtra();
        await LoadPostsAsync();
    }

    private async Task RemoveFilterLocation()
    {
        appliedLocationId = null;
        draftLocationId = null;
        currentPage = 1;
        UpdateHeaderSearchExtra();
        await LoadPostsAsync();
    }

    private async Task ResetAllSearchAndFilters()
    {
        searchInput = string.Empty;
        Shell.SearchTerm = string.Empty;
        appliedSearchTerm = null;
        appliedPostType = null;
        draftPostType = null;
        appliedLocationId = null;
        draftLocationId = null;
        currentPage = 1;
        UpdateHeaderSearchExtra();
        await LoadPostsAsync();
    }

    private string FormatPostType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return string.Empty;
        var clean = type.Trim().Replace(" ", "").ToLowerInvariant();
        return clean switch
        {
            "normal" => "Normal",
            "findcoach" => "Find Coach",
            "findtrainee" => "Find Trainee",
            _ => type.Trim()
        };
    }

    private string GetLocationName(Guid locationId)
    {
        var loc = availableLocations.FirstOrDefault(l => l.LocationId == locationId);
        return loc?.LocationName ?? "Selected Location";
    }

    // Post Interactions
    private readonly HashSet<Guid> _pendingReactionPostIds = new();

    private async Task ToggleLike(PostDto post)
    {
        if (post == null || _pendingReactionPostIds.Contains(post.Id))
        {
            return;
        }

        _pendingReactionPostIds.Add(post.Id);

        try
        {
            var response = await PostService.ToggleLikeAsync(post.Id);
            if (response.Success && response.Data != null)
            {
                post.IsLikedByCurrentUser = response.Data.IsLiked;
                post.LikeCount = response.Data.LikeCount;
                StateHasChanged();
            }
            else if (!response.Success && !string.IsNullOrWhiteSpace(response.Message))
            {
                Console.WriteLine($"[Home] Failed to react to post {post.Id}: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Home] Exception toggling like for post {post.Id}: {ex.Message}");
        }
        finally
        {
            _pendingReactionPostIds.Remove(post.Id);
        }
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

    private void NavigateToAuthorProfile(Guid authorId)
    {
        if (authorId == Guid.Empty) return;

        if (currentUserId.HasValue && authorId == currentUserId.Value)
        {
            Navigation.NavigateTo("/profile");
        }
        else
        {
            Navigation.NavigateTo($"/profile/{authorId}");
        }
    }

    // Realtime Post Deletion Handler
    private void HandlePostDeletedRealtime(Guid deletedPostId)
    {
        _ = InvokeAsync(() =>
        {
            if (posts == null || deletedPostId == Guid.Empty) return;

            var removed = posts.RemoveAll(p => p.Id == deletedPostId);
            if (removed > 0)
            {
                posts = new List<PostDto>(posts);
                totalCount = Math.Max(0, totalCount - removed);

                if (editingPost?.Id == deletedPostId)
                {
                    CloseEditPostModal();
                    ShowWarningToast("The post you were editing has been removed by an administrator.");
                }

                if (postToDelete?.Id == deletedPostId)
                {
                    CloseDeleteModal();
                    ShowWarningToast("This post has already been removed.");
                }

                if (reportPostId == deletedPostId)
                {
                    CloseReportModal();
                    ShowWarningToast("The post you were reporting has been removed.");
                }

                StateHasChanged();
            }
        });
    }

    // Dispose
    public void Dispose()
    {
        PostHubClient.OnPostDeleted -= HandlePostDeletedRealtime;
        Shell.Reset();
        toastCts?.Cancel();
        toastCts?.Dispose();
        warningToastCts?.Cancel();
        warningToastCts?.Dispose();
    }
}
