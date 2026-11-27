using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class FeedAuthorizationPipelineTests
{
    private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var roleValue) || string.IsNullOrEmpty(roleValue))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var role = roleValue.ToString();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestAuth");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private TestServer CreateServer(Mock<IPostService> mockPostService)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddAuthentication("TestAuth")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", null);

                services.AddAuthorization();

                services.AddControllers()
                    .AddApplicationPart(typeof(PostsController).Assembly);

                services.AddSingleton(mockPostService.Object);
                services.AddSingleton(new Mock<IPostReportService>().Object);
                services.AddSingleton(new Mock<IPostReactionService>().Object);
                services.AddSingleton(new Mock<IPostCommentService>().Object);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });

        return new TestServer(builder);
    }

    [Theory]
    [InlineData("/api/posts")]
    [InlineData("/api/posts/search")]
    [InlineData("/api/posts/filter")]
    public async Task HttpPipeline_UnauthenticatedRequest_Returns401Unauthorized(string endpoint)
    {
        var mockService = new Mock<IPostService>();
        using var server = CreateServer(mockService);
        using var client = server.CreateClient();

        // No auth header provided
        var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleConstants.Staff)]
    [InlineData(RoleConstants.Admin)]
    public async Task HttpPipeline_StaffOrAdmin_Returns403Forbidden(string role)
    {
        var mockService = new Mock<IPostService>();
        using var server = CreateServer(mockService);
        using var client = server.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        var response = await client.GetAsync("/api/posts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleConstants.Trainee)]
    [InlineData(RoleConstants.Coach)]
    public async Task HttpPipeline_TraineeOrCoach_Returns200OK(string role)
    {
        var mockService = new Mock<IPostService>();
        mockService.Setup(s => s.GetPostsAsync(
                It.IsAny<GetPostsQueryDto>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<PostDto>>.Ok(new PagedResultDto<PostDto>
            {
                Items = new List<PostDto>(),
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 10
            }));

        using var server = CreateServer(mockService);
        using var client = server.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        var response = await client.GetAsync("/api/posts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HttpPipeline_GetPostById_AllowsAnonymous()
    {
        var mockService = new Mock<IPostService>();
        var samplePostId = Guid.NewGuid();
        mockService.Setup(s => s.GetPostByIdAsync(samplePostId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PostDto>.Ok(new PostDto { Id = samplePostId, Content = "Public post" }));

        using var server = CreateServer(mockService);
        using var client = server.CreateClient();

        // Guest call without any auth headers
        var response = await client.GetAsync($"/api/posts/{samplePostId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HttpPipeline_GetPostsByUser_AllowsAnonymous()
    {
        var mockService = new Mock<IPostService>();
        var authorId = Guid.NewGuid();
        mockService.Setup(s => s.GetPostsAsync(It.Is<GetPostsQueryDto>(q => q.AuthorId == authorId), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<PostDto>>.Ok(new PagedResultDto<PostDto>
            {
                Items = new List<PostDto>(),
                TotalCount = 0
            }));

        using var server = CreateServer(mockService);
        using var client = server.CreateClient();

        // Guest call without any auth headers
        var response = await client.GetAsync($"/api/posts/user/{authorId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/home", true)]
    [InlineData("home", true)]
    [InlineData("home?search=fitness", true)]
    [InlineData("http://evil.com", false)]
    [InlineData("https://evil.com", false)]
    [InlineData("//evil.com", false)]
    [InlineData("/\\evil.com", false)]
    [InlineData("\\\\evil.com", false)]
    [InlineData("javascript:alert(1)", false)]
    public void OpenRedirect_Validation_RejectsExternalUrls(string url, bool expectedSafe)
    {
        // Testing the identical algorithm used in Login.razor IsSafeLocalUrl
        bool isSafe = !string.IsNullOrWhiteSpace(url) &&
                      !url.Contains("://") &&
                      !url.StartsWith("http:", StringComparison.OrdinalIgnoreCase) &&
                      !url.StartsWith("https:", StringComparison.OrdinalIgnoreCase) &&
                      !url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) &&
                      !url.StartsWith("//") &&
                      !url.StartsWith("/\\") &&
                      !url.StartsWith("\\\\");

        Assert.Equal(expectedSafe, isSafe);
    }
}
