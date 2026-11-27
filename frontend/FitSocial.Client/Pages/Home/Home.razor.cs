using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Posts;
using FitSocial.Client.Models.Locations;
using FitSocial.Client.Models.Coaches;
using FitSocial.Client.Components.Posts;

namespace FitSocial.Client.Pages.Home;

public partial class Home : ComponentBase, IAsyncDisposable
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

    // Trending Feed & Infinite Scroll Prefetch state
    private DotNetObjectReference<Home>? dotNetHelper;
    private DateTime? feedSessionAsOf;
    private string? feedSessionId;
    private readonly HashSet<Guid> loadedPostIds = new();
    private Guid? currentTriggerPostId;
    private bool isPrefetching = false;
    private bool prefetchError = false;
    private string? prefetchErrorMessage;
    private bool _needToAttachObserver = false;
    private bool _hasRecoveredFromExpiredSession = false;
    private int _consecutiveEmptyBatches = 0;

    private GetPostsQuery currentQuery = new()
    {
        PageNumber = 1,
        PageSize = 10,
        SortBy = "hot"
    };

    // Lifecycle
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Uri.EscapeDataString(Navigation.ToBaseRelativePath(Navigation.Uri));
            Navigation.NavigateTo(string.IsNullOrEmpty(returnUrl) ? "login" : $"login?returnUrl={returnUrl}");
            return;
        }

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
        Shell.SearchPlaceholder = "Search posts...";
        UpdateHeaderSearchExtra();

        await LoadUserInfoAsync();

        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
        {
            var returnUrl = Uri.EscapeDataString(Navigation.ToBaseRelativePath(Navigation.Uri));
            Navigation.NavigateTo(string.IsNullOrEmpty(returnUrl) ? "login" : $"login?returnUrl={returnUrl}");
            return;
        }

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                dotNetHelper = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("fitSocialInfiniteScroll.init", dotNetHelper);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Home] Error initializing infinite scroll: {ex.Message}");
            }
        }

        if (_needToAttachObserver)
        {
            _needToAttachObserver = false;
            if (currentTriggerPostId.HasValue && hasNextPage)
            {
                try
                {
                    await JS.InvokeVoidAsync("fitSocialInfiniteScroll.observeTrigger", $"post-prefetch-trigger-{currentTriggerPostId.Value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Home] Error observing prefetch trigger: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    await JS.InvokeVoidAsync("fitSocialInfiniteScroll.unobserveCurrent");
                }
                catch
                {
                    // Ignored
                }
            }
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
    private async Task LoadPostsAsync(bool append = false, bool showSkeleton = true)
    {
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
        {
            var returnUrl = Uri.EscapeDataString(Navigation.ToBaseRelativePath(Navigation.Uri));
            Navigation.NavigateTo(string.IsNullOrEmpty(returnUrl) ? "login" : $"login?returnUrl={returnUrl}");
            return;
        }

        var requestId = Interlocked.Increment(ref _loadPostsRequestId);

        if (!append)
        {
            if (showSkeleton)
            {
                isLoading = true;
            }
            isLoadingMore = false;
            isPrefetching = false;
            _hasRecoveredFromExpiredSession = false;
            _consecutiveEmptyBatches = 0;
            currentPage = 1;
            feedSessionAsOf = DateTime.UtcNow;
            feedSessionId = Guid.NewGuid().ToString("N");
            loadedPostIds.Clear();
            currentTriggerPostId = null;
            prefetchError = false;
            prefetchErrorMessage = null;
        }
        else
        {
            isLoadingMore = true;
        }

        currentQuery.PageNumber = currentPage;
        currentQuery.PageSize = pageSize;
        currentQuery.SortBy = "hot";
        currentQuery.AsOf = feedSessionAsOf;
        currentQuery.SessionId = feedSessionId;
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
                if (!string.IsNullOrWhiteSpace(response.Data.SessionId))
                {
                    feedSessionId = response.Data.SessionId;
                    currentQuery.SessionId = feedSessionId;
                }

                var incoming = response.Data.Items ?? new List<PostDto>();
                if (append && posts != null)
                {
                    var unique = incoming.Where(p => loadedPostIds.Add(p.Id)).ToList();
                    posts.AddRange(unique);
                }
                else
                {
                    loadedPostIds.Clear();
                    foreach (var p in incoming)
                    {
                        loadedPostIds.Add(p.Id);
                    }
                    posts = incoming;
                }

                totalCount = response.Data.TotalCount;
                hasNextPage = response.Data.HasNextPage;
                UpdateTriggerPost();
                _needToAttachObserver = true;

                // Handle initial empty batch (Page 1):
                // If Page 1 returned 0 posts (e.g. all 10 posts in Page 1 were deleted),
                // but hasNextPage is true, no post DOM elements exist to trigger IntersectionObserver.
                // We proactively fetch the next batch up to the consecutive empty batch limit (3).
                if (!append && incoming.Count == 0 && hasNextPage)
                {
                    _consecutiveEmptyBatches++;
                    if (_consecutiveEmptyBatches < 3)
                    {
                        isLoading = false;
                        StateHasChanged();
                        await LoadNextBatchAsync();
                        return;
                    }
                }
            }
            else
            {
                var isAuthError = response.Message != null &&
                                  (response.Message.Contains("authenticated", StringComparison.OrdinalIgnoreCase) ||
                                   response.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                   response.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase));
                if (isAuthError)
                {
                    var returnUrl = Uri.EscapeDataString(Navigation.ToBaseRelativePath(Navigation.Uri));
                    Navigation.NavigateTo(string.IsNullOrEmpty(returnUrl) ? "login" : $"login?returnUrl={returnUrl}");
                    return;
                }

                if (!append)
                {
                    posts = new List<PostDto>();
                }
                hasNextPage = false;
                currentTriggerPostId = null;
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
            currentTriggerPostId = null;
        }
        finally
        {
            if (requestId == _loadPostsRequestId)
            {
                isLoading = false;
                if (!isPrefetching)
                {
                    isLoadingMore = false;
                }
                StateHasChanged();
            }
        }
    }

    private void UpdateTriggerPost()
    {
        if (!hasNextPage || posts == null || posts.Count == 0)
        {
            currentTriggerPostId = null;
            return;
        }

        // The trigger is the 7th post of the latest loaded batch:
        // For currentPage = 1 (10 posts) -> index 6 (7th post)
        // For currentPage = 2 (20 posts) -> index 16 (17th post)
        // For currentPage = 3 (30 posts) -> index 26 (27th post)
        var targetIndex = (currentPage * pageSize) - 4;
        if (targetIndex >= 0 && targetIndex < posts.Count)
        {
            currentTriggerPostId = posts[targetIndex].Id;
        }
        else if (posts.Count >= 7)
        {
            currentTriggerPostId = posts[posts.Count - 4].Id;
        }
        else
        {
            // Short batch case: fewer than 7 total posts due to deleted items, but hasNextPage is still true.
            // Trigger on the second-to-last or last item so prefetching is not halted.
            var shortBatchTriggerIndex = Math.Max(0, posts.Count - 2);
            currentTriggerPostId = posts[shortBatchTriggerIndex].Id;
        }
    }

    [JSInvokable]
    public async Task OnPrefetchTriggered()
    {
        if (isLoading || isLoadingMore || isPrefetching || !hasNextPage)
        {
            return;
        }

        await LoadNextBatchAsync();
    }

    private async Task LoadNextBatchAsync()
    {
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty || isLoadingMore || isPrefetching || !hasNextPage)
            return;

        isPrefetching = true;
        isLoadingMore = true;
        prefetchError = false;
        prefetchErrorMessage = null;
        StateHasChanged();

        var requestId = _loadPostsRequestId;
        var sessionId = feedSessionId;
        var nextPage = currentPage + 1;
        var query = new GetPostsQuery
        {
            PageNumber = nextPage,
            PageSize = pageSize,
            SortBy = "hot",
            AsOf = feedSessionAsOf,
            SessionId = sessionId,
            SearchTerm = string.IsNullOrWhiteSpace(appliedSearchTerm) ? null : appliedSearchTerm.Trim(),
            PostType = string.IsNullOrWhiteSpace(appliedPostType) ? null : appliedPostType,
            LocationId = appliedLocationId.HasValue && appliedLocationId != Guid.Empty ? appliedLocationId : null
        };

        try
        {
            var response = await PostService.GetPostsAsync(query);
            if (requestId != _loadPostsRequestId || sessionId != feedSessionId)
            {
                // Discard stale response from superseded request/filter session
                return;
            }

            if (response.Success && response.Data != null)
            {
                var incoming = response.Data.Items ?? new List<PostDto>();
                if (incoming.Any() && posts != null)
                {
                    _consecutiveEmptyBatches = 0;
                    var unique = incoming.Where(p => loadedPostIds.Add(p.Id)).ToList();
                    posts.AddRange(unique);
                }
                else
                {
                    _consecutiveEmptyBatches++;
                }

                currentPage = nextPage;
                totalCount = response.Data.TotalCount;
                hasNextPage = response.Data.HasNextPage;
                UpdateTriggerPost();

                // If batch was empty (all posts in page slice deleted) but more pages remain in snapshot,
                // auto-advance up to 3 times to prevent feed stall without infinite looping.
                if (incoming.Count == 0 && hasNextPage && _consecutiveEmptyBatches < 3)
                {
                    isPrefetching = false;
                    isLoadingMore = false;
                    await LoadNextBatchAsync();
                    return;
                }
            }
            else
            {
                var isAuthError = response.Message != null &&
                                  (response.Message.Contains("authenticated", StringComparison.OrdinalIgnoreCase) ||
                                   response.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                   response.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase));
                if (isAuthError)
                {
                    var returnUrl = Uri.EscapeDataString(Navigation.ToBaseRelativePath(Navigation.Uri));
                    Navigation.NavigateTo(string.IsNullOrEmpty(returnUrl) ? "login" : $"login?returnUrl={returnUrl}");
                    return;
                }

                var isExpired = response.Message != null &&
                                (response.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
                                 (response.Message.Contains("session", StringComparison.OrdinalIgnoreCase) && response.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase)));

                if (isExpired && !_hasRecoveredFromExpiredSession)
                {
                    _hasRecoveredFromExpiredSession = true;
                    // Automatically restart a fresh feed session from Page 1 to recover cleanly
                    await LoadPostsAsync(append: false);
                    return;
                }

                prefetchError = true;
                prefetchErrorMessage = isExpired
                    ? "Your feed session has expired. Click Retry to refresh your feed."
                    : (response.Message ?? "Failed to load more posts.");
            }
        }
        catch (Exception ex)
        {
            if (requestId != _loadPostsRequestId || sessionId != feedSessionId)
            {
                return;
            }

            Console.WriteLine($"[Home] Error prefetching posts: {ex.Message}");
            prefetchError = true;
            prefetchErrorMessage = "Failed to load more posts. Please check your connection.";
        }
        finally
        {
            if (requestId == _loadPostsRequestId && sessionId == feedSessionId)
            {
                isPrefetching = false;
                isLoadingMore = false;
                _needToAttachObserver = true;
                StateHasChanged();
            }
        }
    }

    private async Task RetryPrefetchAsync()
    {
        if (prefetchErrorMessage != null && prefetchErrorMessage.Contains("expired", StringComparison.OrdinalIgnoreCase))
        {
            prefetchError = false;
            prefetchErrorMessage = null;
            await LoadPostsAsync(append: false);
        }
        else
        {
            prefetchError = false;
            prefetchErrorMessage = null;
            await LoadNextBatchAsync();
        }
    }

    private async Task LoadMorePostsAsync()
    {
        await LoadNextBatchAsync();
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

    private async Task OnPostCreated(PostDto newPost)
    {
        showCreatePostModal = false;
        createPostInitialType = null;
        ShowSuccessToast("Post published successfully!");

        // If search or filter is active and does not match the new post, reset search/filters
        // so that the newly created post is visible at the top of the feed
        bool matchesSearch = string.IsNullOrWhiteSpace(appliedSearchTerm) ||
            (newPost.Content?.Contains(appliedSearchTerm, StringComparison.OrdinalIgnoreCase) ?? false);
        bool matchesType = string.IsNullOrWhiteSpace(appliedPostType) ||
            string.Equals(NormalizePostType(newPost.PostType), NormalizePostType(appliedPostType), StringComparison.OrdinalIgnoreCase);
        bool matchesLocation = !appliedLocationId.HasValue || appliedLocationId == Guid.Empty ||
            newPost.LocationId == appliedLocationId.Value;

        if (!matchesSearch || !matchesType || !matchesLocation)
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
            UpdateHeaderSearchExtra();

            // Synchronize browser URL by removing search & filter query parameters while preserving any unrelated query parameters
            try
            {
                var newUri = Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
                {
                    ["search"] = null,
                    ["searchTerm"] = null,
                    ["postType"] = null,
                    ["locationId"] = null
                });
                Navigation.NavigateTo(newUri, replace: true);
            }
            catch
            {
                Navigation.NavigateTo("home", replace: true);
            }
        }

        // Set PrioritizedPostId so backend snapshot places newPost at index 0 of Page 1
        currentQuery.PrioritizedPostId = newPost.Id;

        // Optimistically prepend new post to feed for instant visual response without skeleton flash
        posts ??= new List<PostDto>();
        posts.RemoveAll(p => p.Id == newPost.Id);
        posts.Insert(0, newPost);
        loadedPostIds.Add(newPost.Id);
        totalCount++;
        StateHasChanged();

        // Refresh feed session: backend will place newPost at index 0 of snapshot.PostIds,
        // guaranteeing consistent pagination and infinite scroll across all pages.
        await LoadPostsAsync(append: false, showSkeleton: false);

        currentQuery.PrioritizedPostId = null;
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
        loadedPostIds.Remove(deletedPostId);
        totalCount = Math.Max(0, totalCount - 1);
        UpdateTriggerPost();
        _needToAttachObserver = true;
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
                loadedPostIds.Remove(deletedPostId);
                posts = new List<PostDto>(posts);
                totalCount = Math.Max(0, totalCount - removed);
                UpdateTriggerPost();
                _needToAttachObserver = true;

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
    public async ValueTask DisposeAsync()
    {
        PostHubClient.OnPostDeleted -= HandlePostDeletedRealtime;
        Shell.Reset();
        toastCts?.Cancel();
        toastCts?.Dispose();
        warningToastCts?.Cancel();
        warningToastCts?.Dispose();

        try
        {
            await JS.InvokeVoidAsync("fitSocialInfiniteScroll.dispose");
        }
        catch
        {
            // Ignored on teardown
        }

        dotNetHelper?.Dispose();
        dotNetHelper = null;
    }
}
