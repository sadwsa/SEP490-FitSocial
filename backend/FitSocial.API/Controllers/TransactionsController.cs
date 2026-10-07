using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Transactions;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;

    public TransactionsController(ITransactionService service)
    {
        _service = service;
    }

    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyTransactionHistory([FromQuery] TransactionFilterDto filter)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "TRAINEE";

        if (!Guid.TryParse(userIdString, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<PagedResultDto<TransactionHistoryDto>>.Fail("Invalid token or user ID"));
        }

        var result = await _service.GetMyTransactionsAsync(currentUserId, role, filter);
        return Ok(ApiResponseDto<PagedResultDto<TransactionHistoryDto>>.Ok(result));
    }
}
