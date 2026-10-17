namespace FitSocial.Application.DTOs.Payments;

/// <summary>
/// Coach activation (UC): check the PayOS payment by order code.
/// </summary>
public record CompleteActivationRequest(long OrderCode);

/// <summary>
/// Coach activation (UC): cancel a pending activation order.
/// </summary>
public record CancelActivationRequest(long OrderCode);

/// <summary>
/// UC-20: purchase a training package (prepare / create-link input).
/// </summary>
public record PackagePurchaseRequest(Guid PackageId);

/// <summary>
/// UC-20: verify the PayOS payment for a training package order.
/// </summary>
public record CompletePackagePurchaseRequest(long OrderCode);

/// <summary>
/// UC-20: cancel a pending package purchase.
/// </summary>
public record CancelPackagePurchaseRequest(long OrderCode);
