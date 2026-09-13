using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class BodyMetricLog
{
    public Guid BodyMetricLogId { get; set; }

    public Guid TraineeId { get; set; }

    public DateTime LogDate { get; set; }

    public decimal? BodyWeightKg { get; set; }

    public int? CalorieIntake { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<BodyMeasurementDetail> BodyMeasurementDetails { get; set; } = new List<BodyMeasurementDetail>();

    public virtual User Trainee { get; set; } = null!;
}
