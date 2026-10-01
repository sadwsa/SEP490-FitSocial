using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Order
{
    public Guid OrderId { get; set; }

    public Guid BuyerId { get; set; }

    public Guid? CoachId { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? OrderStatus { get; set; }

    public string? OrderType { get; set; }

    public DateTime? CreatedAt { get; set; }

    // Backward compatibility property
    public Guid TraineeId
    {
        get => BuyerId;
        set => BuyerId = value;
    }

    public virtual User Buyer { get; set; } = null!;

    // Backward compatibility navigation
    public virtual User Trainee
    {
        get => Buyer;
        set => Buyer = value;
    }

    public virtual CoachProfile? Coach { get; set; }

    public virtual CoachUpgrade? CoachUpgrade { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PayoutItem> PayoutItems { get; set; } = new List<PayoutItem>();

    public virtual ICollection<RefundRequest> RefundRequests { get; set; } = new List<RefundRequest>();
}
