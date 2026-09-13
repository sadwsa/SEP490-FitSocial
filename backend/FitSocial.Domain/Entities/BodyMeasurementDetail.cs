using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class BodyMeasurementDetail
{
    public Guid BodyMeasurementDetailId { get; set; }

    public Guid BodyMetricLogId { get; set; }

    public string? MeasurementType { get; set; }

    public decimal? ValueCm { get; set; }

    public virtual BodyMetricLog BodyMetricLog { get; set; } = null!;
}
