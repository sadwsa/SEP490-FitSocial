using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class CoachProfile
{
    public Guid CoachId { get; set; }

    public int? ExperienceYears { get; set; }

    public string? Bio { get; set; }

    public string? IdentityCardUrl { get; set; }

    public string? CertificateUrl { get; set; }

    public string? ApprovalStatus { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User Coach { get; set; } = null!;

    public virtual ICollection<CoachBankAccount> CoachBankAccounts { get; set; } = new List<CoachBankAccount>();

    public virtual ICollection<CoachUpgrade> CoachUpgrades { get; set; } = new List<CoachUpgrade>();

    public virtual ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payout> Payouts { get; set; } = new List<Payout>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<TrainingPackage> TrainingPackages { get; set; } = new List<TrainingPackage>();

    public virtual ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();

    public virtual ICollection<VideoTutorial> VideoTutorials { get; set; } = new List<VideoTutorial>();

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();

    public virtual ICollection<Sport> Sports { get; set; } = new List<Sport>();
}
