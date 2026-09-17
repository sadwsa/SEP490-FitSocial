using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Yêu cầu đăng nhập để xem danh sách Coach
public class CoachesController : ControllerBase
{
    private readonly ICoachService _coachService;

    public CoachesController(ICoachService coachService)
    {
        _coachService = coachService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCoaches()
    {
        var result = await _coachService.GetAllCoachesAsync();
        return Ok(result);
    }
}
