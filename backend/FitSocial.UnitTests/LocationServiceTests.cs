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
using Microsoft.AspNetCore.Http;
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

    #region Create Location Tests - Happy Path

    [Fact]
    public async Task CreateLocationAsync_ValidData_ShouldCreateLocationSuccessfully()
    {
        // Arrange
        var dto = new CreateLocationDto
        {
            LocationName = "Saigon Central Fitness",
            Address = "123 Le Loi, D1, HCMC"
        };

        _locationRepoMock.Setup(r => r.GetByNameAsync("Saigon Central Fitness", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);
        _locationRepoMock.Setup(r => r.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Location created successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("Saigon Central Fitness", result.Data.LocationName);
        Assert.Equal("123 Le Loi, D1, HCMC", result.Data.Address);
        Assert.NotEqual(Guid.Empty, result.Data.LocationId);

        _locationRepoMock.Verify(r => r.AddAsync(It.Is<Location>(l => l.LocationName == "Saigon Central Fitness" && l.Address == "123 Le Loi, D1, HCMC"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLocationAsync_WithLeadingAndTrailingWhitespace_ShouldTrimBeforeSaving()
    {
        // Arrange
        var dto = new CreateLocationDto
        {
            LocationName = "   Hanoi Fitness Club   ",
            Address = "   456 Ba Dinh, Hanoi   "
        };

        _locationRepoMock.Setup(r => r.GetByNameAsync("Hanoi Fitness Club", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);
        _locationRepoMock.Setup(r => r.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Hanoi Fitness Club", result.Data!.LocationName);
        Assert.Equal("456 Ba Dinh, Hanoi", result.Data.Address);

        _locationRepoMock.Verify(r => r.AddAsync(It.Is<Location>(l => l.LocationName == "Hanoi Fitness Club" && l.Address == "456 Ba Dinh, Hanoi"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LocationsController_CreateLocation_WhenSuccessful_ShouldReturn201Created()
    {
        // Arrange
        var locationServiceMock = new Mock<ILocationService>();
        var dto = new CreateLocationDto { LocationName = "Saigon Fitness", Address = "123 Le Loi" };
        var createdDto = new LocationDto { LocationId = Guid.NewGuid(), LocationName = "Saigon Fitness", Address = "123 Le Loi" };

        locationServiceMock.Setup(s => s.CreateLocationAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<LocationDto>.Ok(createdDto, "Location created successfully."));

        var controller = new LocationsController(locationServiceMock.Object);

        // Act
        var actionResult = await controller.CreateLocation(dto);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<LocationDto>>(objectResult.Value);
        Assert.True(body.Success);
        Assert.Equal("Location created successfully.", body.Message);
    }

    #endregion

    #region Create Location Tests - Negative Cases

    [Fact]
    public async Task CreateLocationAsync_WhenDtoIsNull_ShouldReturnFailureResult()
    {
        // Act
        var result = await _service.CreateLocationAsync(null!);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Request payload cannot be null.", result.Message);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateLocationAsync_WhenLocationNameIsNullOrWhitespace_ShouldReturnFailureResult(string? invalidName)
    {
        // Arrange
        var dto = new CreateLocationDto { LocationName = invalidName!, Address = "Valid Address" };

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Location name cannot be empty.", result.Message);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateLocationAsync_WhenAddressIsNullOrWhitespace_ShouldReturnFailureResult(string? invalidAddress)
    {
        // Arrange
        var dto = new CreateLocationDto { LocationName = "Valid Gym", Address = invalidAddress };

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Address cannot be empty.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CreateLocationAsync_WhenLocationNameAlreadyExists_ShouldReturnFailureResult()
    {
        // Arrange
        var dto = new CreateLocationDto { LocationName = "Existing Gym", Address = "123 Address" };
        var existingLocation = new Location { LocationId = Guid.NewGuid(), LocationName = "Existing Gym", Address = "Old Address" };

        _locationRepoMock.Setup(r => r.GetByNameAsync("Existing Gym", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLocation);

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Location 'Existing Gym' already exists.", result.Message);
        Assert.Null(result.Data);

        _locationRepoMock.Verify(r => r.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LocationsController_CreateLocation_WhenNameAlreadyExists_ShouldReturn409Conflict()
    {
        // Arrange
        var locationServiceMock = new Mock<ILocationService>();
        var dto = new CreateLocationDto { LocationName = "Existing Gym", Address = "123 Le Loi" };

        locationServiceMock.Setup(s => s.CreateLocationAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<LocationDto>.Fail("Location 'Existing Gym' already exists."));

        var controller = new LocationsController(locationServiceMock.Object);

        // Act
        var actionResult = await controller.CreateLocation(dto);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<LocationDto>>(conflictResult.Value);
        Assert.False(body.Success);
        Assert.Contains("already exists", body.Message);
    }

    [Fact]
    public async Task LocationsController_CreateLocation_WhenValidationFails_ShouldReturn400BadRequest()
    {
        // Arrange
        var locationServiceMock = new Mock<ILocationService>();
        var dto = new CreateLocationDto { LocationName = "", Address = "123 Le Loi" };

        locationServiceMock.Setup(s => s.CreateLocationAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<LocationDto>.Fail("Location name cannot be empty."));

        var controller = new LocationsController(locationServiceMock.Object);

        // Act
        var actionResult = await controller.CreateLocation(dto);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<LocationDto>>(badRequestResult.Value);
        Assert.False(body.Success);
    }

    #endregion

    #region Create Location Tests - Boundary Cases

    [Fact]
    public async Task CreateLocationAsync_NameAndAddressExactly200Characters_ShouldSucceed()
    {
        // Arrange
        var name = new string('A', 200);
        var address = new string('B', 200);
        var dto = new CreateLocationDto { LocationName = name, Address = address };

        _locationRepoMock.Setup(r => r.GetByNameAsync(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(200, result.Data!.LocationName.Length);
        Assert.Equal(200, result.Data.Address!.Length);
    }

    [Fact]
    public async Task CreateLocationAsync_WhenNameExceeds200Characters_ShouldReturnFailureResult()
    {
        // Arrange
        var name = new string('A', 201);
        var dto = new CreateLocationDto { LocationName = name, Address = "Valid Address" };

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Location name cannot exceed 200 characters.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CreateLocationAsync_WhenAddressExceeds200Characters_ShouldReturnFailureResult()
    {
        // Arrange
        var address = new string('B', 201);
        var dto = new CreateLocationDto { LocationName = "Valid Name", Address = address };

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Address cannot exceed 200 characters.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CreateLocationAsync_WhenNameWithWhitespaceExceeds200BeforeTrimButUnder200AfterTrim_ShouldSucceed()
    {
        // Arrange
        var rawName = "   " + new string('C', 200) + "   "; // Length 206 before trim, 200 after trim
        var dto = new CreateLocationDto { LocationName = rawName, Address = "Valid Address" };

        _locationRepoMock.Setup(r => r.GetByNameAsync(new string('C', 200), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(200, result.Data!.LocationName.Length);
    }

    #endregion

    #region Create Location Tests - Exception Cases

    [Fact]
    public async Task CreateLocationAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var dto = new CreateLocationDto { LocationName = "Valid Gym", Address = "Valid Address" };

        _locationRepoMock.Setup(r => r.GetByNameAsync("Valid Gym", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection dropped."));

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Failed to create location: DB connection dropped.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CreateLocationAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var dto = new CreateLocationDto { LocationName = "Valid Gym", Address = "Valid Address" };

        _locationRepoMock.Setup(r => r.GetByNameAsync("Valid Gym", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Location?)null);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Deadlock detected during commit."));

        // Act
        var result = await _service.CreateLocationAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Failed to create location: Deadlock detected during commit.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CreateLocationAsync_WhenCancellationRequested_ShouldReturnFailureResult()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var dto = new CreateLocationDto { LocationName = "Valid Gym", Address = "Valid Address" };

        _locationRepoMock.Setup(r => r.GetByNameAsync("Valid Gym", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var result = await _service.CreateLocationAsync(dto, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Failed to create location", result.Message);
    }

    #endregion
}

