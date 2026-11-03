using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for LocationService and LocationsController (specifically View Location List features)
/// covering Happy Path, Negative Case, Boundary Case, Exception Case.
/// </summary>
public class LocationServiceTests
{
    private readonly Mock<ILocationRepository> _locationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LocationService _service;

    public LocationServiceTests()
    {
        _locationRepoMock = new Mock<ILocationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new LocationService(_locationRepoMock.Object, _unitOfWorkMock.Object);
    }

    #region 1. Happy Path Tests

    [Fact]
    public async Task GetPagedLocationsAsync_WithoutSearchTerm_ShouldReturnPagedLocationsWithCalculatedPostCounts()
    {
        // Arrange
        var locations = new List<Location>
        {
            new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = "District 1 Fitness Club",
                Address = "123 Le Loi, D1, HCMC",
                Posts = new List<Post>
                {
                    new Post { Id = Guid.NewGuid(), IsDeleted = false },
                    new Post { Id = Guid.NewGuid(), IsDeleted = false },
                    new Post { Id = Guid.NewGuid(), IsDeleted = true } // Should not be counted
                }
            },
            new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = "District 7 Gym",
                Address = "456 Nguyen Thi Thap, D7, HCMC",
                Posts = new List<Post>
                {
                    new Post { Id = Guid.NewGuid(), IsDeleted = false }
                }
            }
        };

        _locationRepoMock.Setup(r => r.ListLocationsAsync(null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((locations, 2));

        // Act
        var result = await _service.GetPagedLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Locations list retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);

        var firstItem = result.Data.Items[0];
        Assert.Equal(locations[0].LocationId, firstItem.LocationId);
        Assert.Equal("District 1 Fitness Club", firstItem.LocationName);
        Assert.Equal("123 Le Loi, D1, HCMC", firstItem.Address);
        Assert.Equal(2, firstItem.PostCount); // 2 active posts

        var secondItem = result.Data.Items[1];
        Assert.Equal(locations[1].LocationId, secondItem.LocationId);
        Assert.Equal("District 7 Gym", secondItem.LocationName);
        Assert.Equal("456 Nguyen Thi Thap, D7, HCMC", secondItem.Address);
        Assert.Equal(1, secondItem.PostCount);
    }

    [Fact]
    public async Task GetPagedLocationsAsync_WithSearchTerm_ShouldPassSearchTermToRepository()
    {
        // Arrange
        var searchTerm = "District 1";
        var locations = new List<Location>
        {
            new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = "District 1 Fitness Club",
                Address = "123 Le Loi, D1, HCMC",
                Posts = new List<Post>()
            }
        };

        _locationRepoMock.Setup(r => r.ListLocationsAsync(searchTerm, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((locations, 6));

        // Act
        var result = await _service.GetPagedLocationsAsync(searchTerm, pageNumber: 2, pageSize: 5);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(6, result.Data.TotalCount);
        Assert.Equal(2, result.Data.PageNumber);
        Assert.Equal(5, result.Data.PageSize);
        Assert.Single(result.Data.Items);
        Assert.Equal("District 1 Fitness Club", result.Data.Items[0].LocationName);

        _locationRepoMock.Verify(r => r.ListLocationsAsync(searchTerm, 2, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLocationsAsync_ShouldCallGetPagedLocationsAsync_AndReturnSameResult()
    {
        // Arrange
        var searchTerm = "Gym";
        var locations = new List<Location>
        {
            new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = "Central Gym",
                Address = "789 Tran Hung Dao, HCMC",
                Posts = new List<Post>()
            }
        };

        _locationRepoMock.Setup(r => r.ListLocationsAsync(searchTerm, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((locations, 1));

        // Act
        var result = await _service.GetLocationsAsync(searchTerm, 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("Central Gym", result.Data.Items[0].LocationName);
    }

    [Fact]
    public async Task GetPublicLocationsAsync_ShouldReturnAllPublicLocationsWithPostCounts()
    {
        // Arrange
        var locations = new List<Location>
        {
            new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = "Location A",
                Address = "Address A",
                Posts = new List<Post>
                {
                    new Post { Id = Guid.NewGuid(), IsDeleted = false }
                }
            },
            new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = "Location B",
                Address = "Address B",
                Posts = new List<Post>()
            }
        };

        _locationRepoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);

        // Act
        var result = await _service.GetPublicLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Locations list retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("Location A", result.Data[0].LocationName);
        Assert.Equal(1, result.Data[0].PostCount);
        Assert.Equal("Location B", result.Data[1].LocationName);
        Assert.Equal(0, result.Data[1].PostCount);
    }

    [Fact]
    public async Task LocationsController_GetLocations_ShouldFallbackToSearchParameterWhenSearchTermIsNull()
    {
        // Arrange
        var locationServiceMock = new Mock<ILocationService>();
        var pagedResult = PagedResultDto<LocationDto>.Create(new List<LocationDto>(), 0, 1, 10);
        var expectedResponse = ApiResponseDto<PagedResultDto<LocationDto>>.Ok(pagedResult);

        locationServiceMock.Setup(s => s.GetPagedLocationsAsync("FallbackQuery", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new LocationsController(locationServiceMock.Object);

        // Act: searchTerm is null, but search = "FallbackQuery"
        var actionResult = await controller.GetLocations(searchTerm: null, search: "FallbackQuery", pageNumber: 1, pageSize: 10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<LocationDto>>>(okResult.Value);
        Assert.True(body.Success);
        locationServiceMock.Verify(s => s.GetPagedLocationsAsync("FallbackQuery", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 2. Negative Case Tests

    [Fact]
    public async Task GetPagedLocationsAsync_WhenNoLocationsFound_ShouldReturnEmptyPagedResult()
    {
        // Arrange
        _locationRepoMock.Setup(r => r.ListLocationsAsync("NonExistent", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Location>(), 0));

        // Act
        var result = await _service.GetPagedLocationsAsync("NonExistent", 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Equal(0, result.Data.TotalPages);
        Assert.False(result.Data.HasNextPage);
        Assert.False(result.Data.HasPreviousPage);
    }

    [Fact]
    public async Task GetPublicLocationsAsync_WhenNoLocationsExist_ShouldReturnEmptyList()
    {
        // Arrange
        _locationRepoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Location>());

        // Act
        var result = await _service.GetPublicLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    #endregion

    #region 3. Boundary Case Tests

    [Fact]
    public async Task GetPagedLocationsAsync_PaginationBoundaries_ShouldCalculateTotalPagesAndFlagsCorrectly()
    {
        // Arrange
        var totalCount = 25;
        var pageSize = 10;
        var locations = new List<Location> { new Location { LocationId = Guid.NewGuid(), LocationName = "Test Location" } };

        // Test Page 1 of 3
        _locationRepoMock.Setup(r => r.ListLocationsAsync(null, 1, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((locations, totalCount));

        var resultPage1 = await _service.GetPagedLocationsAsync(null, pageNumber: 1, pageSize: pageSize);
        Assert.Equal(3, resultPage1.Data!.TotalPages); // Ceil(25 / 10) = 3
        Assert.True(resultPage1.Data.HasNextPage);
        Assert.False(resultPage1.Data.HasPreviousPage);

        // Test Page 2 of 3 (Middle page)
        _locationRepoMock.Setup(r => r.ListLocationsAsync(null, 2, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((locations, totalCount));

        var resultPage2 = await _service.GetPagedLocationsAsync(null, pageNumber: 2, pageSize: pageSize);
        Assert.True(resultPage2.Data!.HasNextPage);
        Assert.True(resultPage2.Data.HasPreviousPage);

        // Test Page 3 of 3 (Last page)
        _locationRepoMock.Setup(r => r.ListLocationsAsync(null, 3, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((locations, totalCount));

        var resultPage3 = await _service.GetPagedLocationsAsync(null, pageNumber: 3, pageSize: pageSize);
        Assert.False(resultPage3.Data!.HasNextPage);
        Assert.True(resultPage3.Data.HasPreviousPage);
    }

    [Fact]
    public async Task GetPagedLocationsAsync_WhenLocationHasNullPostsOrNullAddress_ShouldHandleSafelyWithoutError()
    {
        // Arrange: Location with null Posts collection and null Address
        var locations = new List<Location>
        {
            new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = "Minimal Location",
                Address = null,
                Posts = null!
            }
        };

        _locationRepoMock.Setup(r => r.ListLocationsAsync(null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((locations, 1));

        // Act
        var result = await _service.GetPagedLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        var item = result.Data.Items[0];
        Assert.Equal("Minimal Location", item.LocationName);
        Assert.Null(item.Address);
        Assert.Equal(0, item.PostCount); // Should default to 0 gracefully without NullReferenceException
    }

    [Fact]
    public async Task GetPagedLocationsAsync_PostCountCalculation_ShouldOnlyCountNonDeletedPosts()
    {
        // Arrange
        var location = new Location
        {
            LocationId = Guid.NewGuid(),
            LocationName = "Multi-post Location",
            Address = "Anywhere",
            Posts = new List<Post>
            {
                new Post { Id = Guid.NewGuid(), IsDeleted = false }, // counted
                new Post { Id = Guid.NewGuid(), IsDeleted = null },  // counted (p.IsDeleted != true is true for null)
                new Post { Id = Guid.NewGuid(), IsDeleted = true },  // excluded
                new Post { Id = Guid.NewGuid(), IsDeleted = true }   // excluded
            }
        };

        _locationRepoMock.Setup(r => r.ListLocationsAsync(null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Location> { location }, 1));

        // Act
        var result = await _service.GetPagedLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Items[0].PostCount);
    }

    #endregion

    #region 4. Exception Case Tests

    [Fact]
    public async Task GetPagedLocationsAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        _locationRepoMock.Setup(r => r.ListLocationsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection timeout."));

        // Act
        var result = await _service.GetPagedLocationsAsync("test", 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Failed to retrieve locations: Database connection timeout.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetPagedLocationsAsync_WhenCancellationRequested_ShouldReturnFailureResult()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _locationRepoMock.Setup(r => r.ListLocationsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var result = await _service.GetPagedLocationsAsync("test", 1, 10, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Failed to retrieve locations", result.Message);
    }

    [Fact]
    public async Task GetPublicLocationsAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        _locationRepoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database down."));

        // Act
        var result = await _service.GetPublicLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Failed to retrieve public locations: Database down.", result.Message);
        Assert.Null(result.Data);
    }

    #endregion
}

