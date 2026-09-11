using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Posts;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Posts;

public interface IPostService
{
    Task<ApiResponse<List<PostDto>>> GetFeedPostsAsync(int page = 1, int pageSize = 10);
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

    public async Task<ApiResponse<List<PostDto>>> GetFeedPostsAsync(int page = 1, int pageSize = 10)
    {
        var response = await _apiClient.GetAsync<List<PostDto>>($"posts/feed?page={page}&pageSize={pageSize}");

        // Fallback demo data if backend post endpoint isn't implemented yet
        if (!response.Success || response.Data == null || !response.Data.Any())
        {
            return new ApiResponse<List<PostDto>>
            {
                Success = true,
                Data = GetSamplePosts()
            };
        }

        return response;
    }

    public async Task<ApiResponse<PostDto>> CreatePostAsync(CreatePostRequest request)
    {
        var response = await _apiClient.PostAsync<CreatePostRequest, PostDto>("posts", request);
        if (!response.Success || response.Data == null)
        {
            // Demo fallback
            var newPost = new PostDto
            {
                Id = Guid.NewGuid(),
                AuthorName = "Bạn (Tôi)",
                AuthorAvatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop",
                Content = request.Content,
                CreatedAt = DateTime.UtcNow,
                LikeCount = 0,
                CommentCount = 0,
                IsLikedByCurrentUser = false
            };
            return new ApiResponse<PostDto> { Success = true, Data = newPost };
        }

        return response;
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
                Id = Guid.NewGuid(),
                AuthorName = "Lê Hoàng Gym",
                AuthorAvatar = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=120&auto=format&fit=crop",
                Content = "Hôm nay hoàn thành buổi tập ngực - tay sau (Push Day) cực cháy! 🔥 Bench Press nâng mức tạ mới 90kg x 5 reps. Anh em hôm nay tập nhóm cơ nào rồi?",
                LikeCount = 24,
                CommentCount = 8,
                IsLikedByCurrentUser = true,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new()
            {
                Id = Guid.NewGuid(),
                AuthorName = "Minh Thư Fitness",
                AuthorAvatar = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=120&auto=format&fit=crop",
                Content = "Chạy bộ 5km buổi sáng quanh hồ Tây đón bình minh 🏃‍♀️ Không khí trong lành giúp cả ngày tràn đầy năng lượng tích cực!",
                LikeCount = 42,
                CommentCount = 15,
                IsLikedByCurrentUser = false,
                CreatedAt = DateTime.UtcNow.AddHours(-5)
            }
        };
    }
}
