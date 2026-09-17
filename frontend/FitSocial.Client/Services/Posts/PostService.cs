using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Posts;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Posts;

public interface IPostService
{
    Task<ApiResponse<PagedResult<PostDto>>> GetPostsAsync(GetPostsQuery? query = null);
    Task<ApiResponse<List<PostDto>>> GetFeedPostsAsync(int page = 1, int pageSize = 10);
    Task<ApiResponse<PostDto>> GetPostByIdAsync(Guid id);
    Task<ApiResponse<PostDto>> CreatePostAsync(CreatePostRequest request);
    Task<ApiResponse<bool>> ToggleLikeAsync(Guid postId);
}

public class PostService : IPostService
{
    private readonly ApiClient _apiClient;

    public PostService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<PostDto>>> GetPostsAsync(GetPostsQuery? query = null)
    {
        query ??= new GetPostsQuery();

        var queryParams = new List<string>
        {
            $"pageNumber={query.PageNumber}",
            $"pageSize={query.PageSize}"
        };

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            queryParams.Add($"searchTerm={Uri.EscapeDataString(query.SearchTerm.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query.PostType))
        {
            queryParams.Add($"postType={Uri.EscapeDataString(query.PostType.Trim())}");
        }

        if (query.SportId.HasValue && query.SportId != Guid.Empty)
        {
            queryParams.Add($"sportId={query.SportId.Value}");
        }

        if (query.LocationId.HasValue && query.LocationId != Guid.Empty)
        {
            queryParams.Add($"locationId={query.LocationId.Value}");
        }

        if (query.AuthorId.HasValue && query.AuthorId != Guid.Empty)
        {
            queryParams.Add($"authorId={query.AuthorId.Value}");
        }

        var url = $"posts?{string.Join("&", queryParams)}";
        var response = await _apiClient.GetAsync<PagedResult<PostDto>>(url);

        // If backend returned no posts or is unreachable, fallback to sample English posts
        if (!response.Success || response.Data == null || response.Data.Items == null || !response.Data.Items.Any())
        {
            var sampleItems = GetSamplePosts();
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var term = query.SearchTerm.Trim().ToLowerInvariant();
                sampleItems = sampleItems.Where(p => 
                    (p.Content?.ToLowerInvariant().Contains(term) ?? false) ||
                    (p.AuthorName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (p.SportName?.ToLowerInvariant().Contains(term) ?? false)
                ).ToList();
            }

            return new ApiResponse<PagedResult<PostDto>>
            {
                Success = true,
                Message = "Loaded demo posts",
                Data = new PagedResult<PostDto>
                {
                    Items = sampleItems,
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalCount = sampleItems.Count
                }
            };
        }

        return response;
    }

    public async Task<ApiResponse<List<PostDto>>> GetFeedPostsAsync(int page = 1, int pageSize = 10)
    {
        var result = await GetPostsAsync(new GetPostsQuery { PageNumber = page, PageSize = pageSize });
        return new ApiResponse<List<PostDto>>
        {
            Success = result.Success,
            Message = result.Message,
            Data = result.Data?.Items ?? new List<PostDto>()
        };
    }

    public async Task<ApiResponse<PostDto>> GetPostByIdAsync(Guid id)
    {
        return await _apiClient.GetAsync<PostDto>($"posts/{id}");
    }

    public async Task<ApiResponse<PostDto>> CreatePostAsync(CreatePostRequest request)
    {
        return await _apiClient.PostAsync<CreatePostRequest, PostDto>("posts", request);
    }

    public async Task<ApiResponse<bool>> ToggleLikeAsync(Guid postId)
    {
        return await _apiClient.PostAsync<object, bool>($"posts/{postId}/like", new { });
    }

    private static List<PostDto> GetSamplePosts()
    {
        return new List<PostDto>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                AuthorName = "Coach Alex",
                AuthorAvatarUrl = "https://images.unsplash.com/photo-1568602471122-7832951cc4c5?w=120&auto=format&fit=crop",
                Content = "Had an amazing chest workout session with my clients today. Everyone, keep pushing hard and stay consistent! 💪🔥",
                SportName = "Bodybuilding",
                LocationName = "Downtown Fitness Center, District 1",
                LikeCount = 124,
                CommentCount = 12,
                IsLikedByCurrentUser = false,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                Media = new List<PostMediaDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        MediaUrl = "https://images.unsplash.com/photo-1581009146145-b5ef050c2e1e?w=800&auto=format&fit=crop",
                        MediaType = "IMAGE"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        MediaUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4",
                        MediaType = "VIDEO"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        MediaUrl = "https://images.unsplash.com/photo-1534438327276-14e5300c3a48?w=800&auto=format&fit=crop",
                        MediaType = "IMAGE"
                    }
                }
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                AuthorName = "Minh Thu Fitness",
                AuthorAvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=120&auto=format&fit=crop",
                Content = "Morning 5k sunrise run around West Lake 🏃‍♀️ Fresh morning air gives the best positive energy for the entire productive day!",
                SportName = "Running",
                LocationName = "West Lake Trail, Hanoi",
                LikeCount = 98,
                CommentCount = 15,
                IsLikedByCurrentUser = true,
                CreatedAt = DateTime.UtcNow.AddHours(-5),
                Media = new List<PostMediaDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        MediaUrl = "https://images.unsplash.com/photo-1502224562085-639556652f33?w=800&auto=format&fit=crop",
                        MediaType = "IMAGE"
                    }
                }
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                AuthorName = "Coach Dat",
                AuthorAvatarUrl = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=120&auto=format&fit=crop",
                Content = "Completed the high-volume calisthenics routine today! Mastered strict muscle-ups and human flag progression. Discipline over motivation any day. 🔥",
                SportName = "Calisthenics",
                LocationName = "FitSocial Training Hub, District 7",
                LikeCount = 67,
                CommentCount = 8,
                IsLikedByCurrentUser = false,
                CreatedAt = DateTime.UtcNow.AddHours(-8),
                Media = new List<PostMediaDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        MediaUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4",
                        MediaType = "VIDEO"
                    }
                }
            }
        };
    }
}
