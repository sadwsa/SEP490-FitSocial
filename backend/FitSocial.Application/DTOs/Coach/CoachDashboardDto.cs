using System;
using System.Collections.Generic;

namespace FitSocial.Application.DTOs.Coach;

public class MonthlyStatDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int TotalOrders { get; set; }
}

public class TopTraineeDto
{
    public Guid TraineeId { get; set; }
    public string TraineeName { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int TotalOrders { get; set; }
}

public class CoachDashboardDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public double RetentionRate { get; set; }
    public List<MonthlyStatDto> MonthlyStats { get; set; } = new();
    public List<TopTraineeDto> TopTrainees { get; set; } = new();
}
