using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.Users;
using Microsoft.AspNetCore.Components;

namespace FitSocial.Client.Pages.Admin;

public partial class Profile : ComponentBase
{
    private StaffAdminOwnProfileDto? profile;
    private bool isLoading = true;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        isLoading = true;
        errorMessage = null;
        StateHasChanged();

        try
        {
            var response = await UserService.GetStaffAdminOwnProfileAsync();
            if (response.Success && response.Data != null)
            {
                profile = response.Data;
            }
            else
            {
                errorMessage = !string.IsNullOrWhiteSpace(response.Message)
                    ? response.Message
                    : "Unable to load your profile. Please try again later.";
            }
        }
        catch (Exception)
        {
            errorMessage = "Unable to load your profile. Please try again later.";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}
