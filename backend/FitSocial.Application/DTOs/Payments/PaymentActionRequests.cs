namespace FitSocial.Application.DTOs.Payments;

public record CompleteActivationRequest(long OrderCode);

public record RenewCoachSubscriptionRequest(Guid PlanId);

public record CancelActivationRequest(long OrderCode);

public record PackagePurchaseRequest(Guid PackageId);

public record CompletePackagePurchaseRequest(long OrderCode);

public record CancelPackagePurchaseRequest(long OrderCode);
