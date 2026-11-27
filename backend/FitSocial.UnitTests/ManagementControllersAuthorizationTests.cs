using System;
using System.Linq;
using System.Reflection;
using FitSocial.API.Controllers;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests verifying Controller-level Authorization attributes and role permissions
/// for management and profile endpoints.
/// </summary>
public class ManagementControllersAuthorizationTests
{
    [Fact]
    public void SystemLogsController_HasAuthorizeAttribute_OnlyForAdmin()
    {
        // Act
        var authAttributes = typeof(SystemLogsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        // Assert
        Assert.NotEmpty(authAttributes);
        var roles = authAttributes.First().Roles;
        Assert.NotNull(roles);
        Assert.Contains(RoleConstants.Admin, roles);
        Assert.DoesNotContain(RoleConstants.Staff, roles);
        Assert.DoesNotContain(RoleConstants.Coach, roles);
        Assert.DoesNotContain(RoleConstants.Trainee, roles);
    }

    [Fact]
    public void AdminUsersController_ClassAndStaffEndpoints_RequireProperRoles()
    {
        // Assert class level has Staff and Admin
        var classAuth = typeof(AdminUsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(classAuth);
        Assert.Contains(RoleConstants.Admin, classAuth.Roles!);
        Assert.Contains(RoleConstants.Staff, classAuth.Roles!);

        // Assert specific Staff management endpoints strictly require Admin
        var staffMethods = new[] { "GetStaff", "CreateStaff", "UpdateStaff" };
        foreach (var methodName in staffMethods)
        {
            var method = typeof(AdminUsersController).GetMethod(methodName);
            Assert.NotNull(method);

            var methodAuth = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(methodAuth);
            Assert.Equal(RoleConstants.Admin, methodAuth.Roles);
        }
    }

    [Fact]
    public void UsersController_ProfileEndpoints_EnforceRoleRestrictions()
    {
        // UpdateStaffAdminProfile: STAFF,ADMIN only
        var staffAdminMethod = typeof(UsersController).GetMethod("UpdateStaffAdminProfile");
        Assert.NotNull(staffAdminMethod);

        var staffAdminAuth = staffAdminMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(staffAdminAuth);
        Assert.Contains(RoleConstants.Staff, staffAdminAuth.Roles!);
        Assert.Contains(RoleConstants.Admin, staffAdminAuth.Roles!);
        Assert.DoesNotContain(RoleConstants.Trainee, staffAdminAuth.Roles!);

        // UpdateOwnProfile: TRAINEE,COACH only
        var traineeCoachMethod = typeof(UsersController).GetMethod("UpdateOwnProfile");
        Assert.NotNull(traineeCoachMethod);

        var traineeCoachAuth = traineeCoachMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(traineeCoachAuth);
        Assert.Contains(RoleConstants.Trainee, traineeCoachAuth.Roles!);
        Assert.Contains(RoleConstants.Coach, traineeCoachAuth.Roles!);
        Assert.DoesNotContain(RoleConstants.Admin, traineeCoachAuth.Roles!);
    }

    [Fact]
    public void CoachesController_ApplicationReviewEndpoints_RequireStaffOrAdmin()
    {
        var reviewMethods = new[] { "GetCoachApplications", "ApproveCoachApplication", "RejectCoachApplication" };
        foreach (var methodName in reviewMethods)
        {
            var method = typeof(CoachesController).GetMethod(methodName);
            Assert.NotNull(method);

            var methodAuth = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(methodAuth);
            Assert.Contains(RoleConstants.Staff, methodAuth.Roles!);
            Assert.Contains(RoleConstants.Admin, methodAuth.Roles!);
            Assert.DoesNotContain(RoleConstants.Trainee, methodAuth.Roles!);
        }
    }

    [Fact]
    public void AdminUsersController_GetUsersEndpoint_RequiresStaffOrAdmin()
    {
        // Act: Verify GetUsers method exists and belongs to the controller decorated with [Authorize(Roles = "STAFF,ADMIN")]
        var method = typeof(AdminUsersController).GetMethod("GetUsers");
        Assert.NotNull(method);

        var classAuth = typeof(AdminUsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(classAuth);
        Assert.Contains(RoleConstants.Staff, classAuth.Roles!);
        Assert.Contains(RoleConstants.Admin, classAuth.Roles!);
        Assert.DoesNotContain(RoleConstants.Trainee, classAuth.Roles!);
        Assert.DoesNotContain(RoleConstants.Coach, classAuth.Roles!);
    }

    [Fact]
    public void LocationsController_GetLocationsEndpoint_RequiresStaffOrAdmin()
    {
        // Act: Verify GetLocations method exists and is restricted to Staff and Admin
        var method = typeof(LocationsController).GetMethod("GetLocations");
        Assert.NotNull(method);

        var authAttribute = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttribute);
        Assert.NotNull(authAttribute.Roles);
        Assert.Contains(RoleConstants.Staff, authAttribute.Roles);
        Assert.Contains(RoleConstants.Admin, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Trainee, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Coach, authAttribute.Roles);
    }

    [Fact]
    public void LocationsController_GetPublicLocationsEndpoint_AllowsAnonymous()
    {
        // Act: Verify GetPublicLocations method is public for all users via [AllowAnonymous]
        var method = typeof(LocationsController).GetMethod("GetPublicLocations");
        Assert.NotNull(method);

        var allowAnonymousAttribute = method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Cast<AllowAnonymousAttribute>()
            .FirstOrDefault();

        Assert.NotNull(allowAnonymousAttribute);
    }

    [Fact]
    public void LocationsController_CreateLocationEndpoint_RequiresStaffOrAdmin()
    {
        // Act: Verify CreateLocation method exists, has HttpPost and is restricted to Staff and Admin
        var method = typeof(LocationsController).GetMethod("CreateLocation");
        Assert.NotNull(method);

        var postAttribute = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()
            .FirstOrDefault();
        Assert.NotNull(postAttribute);

        var authAttribute = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttribute);
        Assert.NotNull(authAttribute.Roles);
        Assert.Contains(RoleConstants.Staff, authAttribute.Roles);
        Assert.Contains(RoleConstants.Admin, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Trainee, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Coach, authAttribute.Roles);
    }

    [Fact]
    public void LocationsController_UpdateLocationEndpoint_RequiresStaffOrAdmin()
    {
        // Act: Verify UpdateLocation method exists, has HttpPut and is restricted to Staff and Admin
        var method = typeof(LocationsController).GetMethod("UpdateLocation");
        Assert.NotNull(method);

        var putAttribute = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPutAttribute>()
            .FirstOrDefault();
        Assert.NotNull(putAttribute);
        Assert.Equal("{locationId:guid}", putAttribute.Template);

        var authAttribute = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttribute);
        Assert.NotNull(authAttribute.Roles);
        Assert.Contains(RoleConstants.Staff, authAttribute.Roles);
        Assert.Contains(RoleConstants.Admin, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Trainee, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Coach, authAttribute.Roles);
    }

    [Fact]
    public void ReportsController_ClassLevel_RequiresAuthorize()
    {
        // Act: Verify ReportsController has class-level [Authorize]
        var classAuth = typeof(ReportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(classAuth);
    }

    [Fact]
    public void LocationsController_DeleteLocationEndpoint_RequiresStaffOrAdmin()
    {
        // Act: Verify DeleteLocation method exists, has HttpDelete and is restricted to Staff and Admin
        var method = typeof(LocationsController).GetMethod("DeleteLocation");
        Assert.NotNull(method);

        var deleteAttribute = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>()
            .FirstOrDefault();
        Assert.NotNull(deleteAttribute);
        Assert.Equal("{locationId:guid}", deleteAttribute.Template);

        var authAttribute = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttribute);
        Assert.NotNull(authAttribute.Roles);
        Assert.Contains(RoleConstants.Staff, authAttribute.Roles);
        Assert.Contains(RoleConstants.Admin, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Trainee, authAttribute.Roles);
        Assert.DoesNotContain(RoleConstants.Coach, authAttribute.Roles);
    }

    [Fact]
    public void CartsController_ClassLevel_RequiresTraineeOrCoach()
    {
        // Act: Verify CartsController is restricted to Trainee and Coach
        var classAuth = typeof(CartsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(classAuth);
        Assert.NotNull(classAuth.Roles);
        Assert.Contains(RoleConstants.Trainee, classAuth.Roles);
        Assert.Contains(RoleConstants.Coach, classAuth.Roles);
        Assert.DoesNotContain(RoleConstants.Admin, classAuth.Roles);
        Assert.DoesNotContain(RoleConstants.Staff, classAuth.Roles);
    }

    [Fact]
    public void ReportsController_ReportUserEndpoint_HasHttpPostAttribute()
    {
        // Act: Verify ReportUser method has [HttpPost("user")]
        var method = typeof(ReportsController).GetMethod("ReportUser");
        Assert.NotNull(method);

        var httpPost = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()
            .FirstOrDefault();

        Assert.NotNull(httpPost);
        Assert.Equal("user", httpPost.Template);
    }

    [Fact]
    public void SystemLogsController_ClassLevel_RequiresAdminOnly()
    {
        var classAuth = typeof(SystemLogsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(classAuth);
        Assert.Equal(RoleConstants.Admin, classAuth.Roles);
    }

    [Fact]
    public void CartsController_GetCartEndpoints_HaveHttpGetAttributes()
    {
        // Act: Verify GetCart has HttpGet
        var getCartMethod = typeof(CartsController).GetMethod("GetCart");
        Assert.NotNull(getCartMethod);
        var httpGet = getCartMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()
            .FirstOrDefault();
        Assert.NotNull(httpGet);

        // Act: Verify GetCartCount has HttpGet("count")
        var getCartCountMethod = typeof(CartsController).GetMethod("GetCartCount");
        Assert.NotNull(getCartCountMethod);
        var httpGetCount = getCartCountMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()
            .FirstOrDefault();
        Assert.NotNull(httpGetCount);
        Assert.Equal("count", httpGetCount.Template);
    }

    [Fact]
    public void CartsController_RemoveCartEndpoints_HaveHttpDeleteAttributes()
    {
        // Act: Verify RemoveCartItem has HttpDelete("{cartId:guid}")
        var removeMethod = typeof(CartsController).GetMethod("RemoveCartItem");
        Assert.NotNull(removeMethod);
        var httpDelete = removeMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>()
            .FirstOrDefault();
        Assert.NotNull(httpDelete);
        Assert.Equal("{cartId:guid}", httpDelete.Template);

        // Act: Verify ClearCart has HttpDelete("clear")
        var clearMethod = typeof(CartsController).GetMethod("ClearCart");
        Assert.NotNull(clearMethod);
        var httpDeleteClear = clearMethod.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>()
            .FirstOrDefault();
        Assert.NotNull(httpDeleteClear);
        Assert.Equal("clear", httpDeleteClear.Template);
    }
}
