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
}