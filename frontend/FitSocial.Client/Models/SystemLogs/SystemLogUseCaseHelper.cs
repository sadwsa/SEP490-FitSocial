using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FitSocial.Client.Models.SystemLogs;

public enum LogDetailType
{
    Initial,      // Initial Value
    BeforeDelete, // Value Before Deletion
    Change        // Previous Value / Current Value
}

public class LogFieldChangeItem
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? RawOldValue { get; set; }
    public string? RawNewValue { get; set; }
    public string FormattedOldValue { get; set; } = "—";
    public string FormattedNewValue { get; set; } = "—";
    public bool IsChanged { get; set; }
    public bool IsStatus { get; set; }
    public string? OldStatusBadgeClass { get; set; }
    public string? NewStatusBadgeClass { get; set; }
    public bool IsLongText { get; set; }
}

public class SystemLogUseCaseInfo
{
    public string GroupName { get; set; } = string.Empty;
    public string UseCaseTitle { get; set; } = string.Empty;
    public string RelatedTable { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LogDetailType DetailType { get; set; } = LogDetailType.Change;

    public List<LogFieldChangeItem> FieldChanges { get; set; } = new();
}

public static class SystemLogUseCaseHelper
{
    private static readonly Dictionary<string, string> FieldDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["status"] = "Status",
        ["requestedamount"] = "Requested Amount",
        ["refundamount"] = "Refund Amount",
        ["approvedamount"] = "Approved Amount",
        ["coachpayoutportion"] = "Coach Payout Portion",
        ["originalpaidamount"] = "Original Paid Amount",
        ["buyer"] = "Buyer (Trainee)",
        ["seller"] = "Seller (Coach)",
        ["order"] = "Order Code",
        ["package"] = "Training Package",
        ["actorrole"] = "Actor Role",
        ["staffnote"] = "Staff Note",
        ["policycase"] = "Policy Case",
        ["bankaccount"] = "Receiving Bank Account",
        ["description"] = "Description",
        ["processedat"] = "Processed At",
        ["processedby"] = "Processed By",
        ["refundtransactionref"] = "Refund Transaction Ref",
        ["transactionref"] = "Transaction Ref",
        ["fullname"] = "Full Name",
        ["name"] = "Name",
        ["email"] = "Email Address",
        ["rolecode"] = "Role Code",
        ["role"] = "Role",
        ["phonenumber"] = "Phone Number",
        ["islocked"] = "Account Locked",
        ["isactive"] = "Active Status",
        ["amount"] = "Amount",
        ["currency"] = "Currency",
        ["subscriptionduration"] = "Duration (Days)",
        ["price"] = "Price",
        ["gatewayname"] = "Gateway Name",
        ["clientid"] = "Client ID",
        ["webhookurl"] = "Webhook URL",
        ["locationname"] = "Location Name",
        ["address"] = "Address",
        ["verificationstatus"] = "Verification Status",
        ["verifiedby"] = "Verified By",
        ["verifiedat"] = "Verified At",
        ["rejectedreason"] = "Rejected Reason",
        ["reason"] = "Reason",
        ["appealstatus"] = "Appeal Status",
        ["appealreviewnote"] = "Appeal Review Note",
        ["netpayoutamount"] = "Net Payout Amount",
        ["payoutmonth"] = "Payout Month",
        ["payoutyear"] = "Payout Year",
        ["coachname"] = "Coach Name",
        ["details"] = "Execution Details",
        ["error"] = "Error Message",
        ["errormessage"] = "Error Message",
        ["errorcode"] = "Error Code",
        ["bankcode"] = "Bank Code",
        ["idempotencykey"] = "Idempotency Key",
        ["payoutstatus"] = "Payout Status"
    };

    public static SystemLogUseCaseInfo Resolve(SystemLogItemModel log)
    {
        var action = (log.Action ?? string.Empty).Trim().ToUpperInvariant();
        var entityType = (log.Source ?? string.Empty).Trim();
        var entityId = (log.EntityId ?? string.Empty).Trim();
        var actor = !string.IsNullOrWhiteSpace(log.ActorName) ? log.ActorName : "User";

        // Parse NewValue / OldValue JSON for dynamic fields
        var (newProps, oldProps) = ParseProps(log.NewValue, log.OldValue);

        string GetProp(string key, string fallback = "")
        {
            if (newProps.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
            if (oldProps.TryGetValue(key, out var v2) && !string.IsNullOrWhiteSpace(v2)) return v2;
            return fallback;
        }

        SystemLogUseCaseInfo info;

        // 1. UC_34.1: Create Staff Accounts
        if (action.Contains("CREATE_STAFF") || (action.Contains("CREATE") && (entityType.Equals("Staff", StringComparison.OrdinalIgnoreCase) || (entityType.Equals("User", StringComparison.OrdinalIgnoreCase) && GetProp("role").Contains("STAFF", StringComparison.OrdinalIgnoreCase)))))
        {
            var targetName = GetProp("fullname", GetProp("name", "New Staff Member"));
            var targetEmail = GetProp("email", "staff@fitsocial.com");
            var targetRole = GetProp("rolecode", GetProp("role", "STAFF"));
            info = new SystemLogUseCaseInfo
            {
                GroupName = "I. ACCOUNT MANAGEMENT & ACCESS CONTROL",
                UseCaseTitle = "1. UC_34.1: Create Staff Accounts",
                RelatedTable = "Users",
                ActionCode = "CREATE_STAFF_ACCOUNT",
                EntityType = "Users",
                EntityId = entityId,
                Description = $"{actor} created a new staff account for {targetName} ({targetEmail}) with role [{targetRole}].",
                DetailType = LogDetailType.Initial
            };
        }
        // 2. UC_34.2: Edit Staff Accounts
        else if (action.Contains("UPDATE_STAFF") || (action.Contains("UPDATE") && entityType.Equals("Staff", StringComparison.OrdinalIgnoreCase)))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "I. ACCOUNT MANAGEMENT & ACCESS CONTROL",
                UseCaseTitle = "2. UC_34.2: Edit Staff Accounts",
                RelatedTable = "Users",
                ActionCode = "UPDATE_STAFF_ACCOUNT",
                EntityType = "Users",
                EntityId = entityId,
                Description = $"{actor} updated staff account details (ID: {entityId}). Data has been modified.",
                DetailType = LogDetailType.Change
            };
        }
        // 3. UC_34.3: Lock Staff Account
        else if (action.Contains("LOCK_USER") || action.Contains("LOCK_STAFF") || (action.StartsWith("LOCK") && (entityType.Contains("User", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Staff", StringComparison.OrdinalIgnoreCase))))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "I. ACCOUNT MANAGEMENT & ACCESS CONTROL",
                UseCaseTitle = "3. UC_34.3: Lock Staff Account",
                RelatedTable = "Users (IsLocked = TRUE, LockedBy)",
                ActionCode = "LOCK_USER_ACCOUNT",
                EntityType = "Users",
                EntityId = entityId,
                Description = $"{actor} locked the staff account (ID: {entityId}). The account active state has been changed.",
                DetailType = LogDetailType.Change
            };
        }
        // 4. UC_34.4: Unlock Staff Account
        else if (action.Contains("UNLOCK_USER") || action.Contains("UNLOCK_STAFF") || (action.StartsWith("UNLOCK") && (entityType.Contains("User", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Staff", StringComparison.OrdinalIgnoreCase))))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "I. ACCOUNT MANAGEMENT & ACCESS CONTROL",
                UseCaseTitle = "4. UC_34.4: Unlock Staff Account",
                RelatedTable = "Users (IsLocked = FALSE)",
                ActionCode = "UNLOCK_USER_ACCOUNT",
                EntityType = "Users",
                EntityId = entityId,
                Description = $"{actor} unlocked the staff account (ID: {entityId}). Operational security status has been restored.",
                DetailType = LogDetailType.Change
            };
        }
        // 5. UC_35.1: Update Admin Profile
        else if (action.Contains("UPDATE_PROFILE") || action.Contains("CHANGE_PASSWORD") || (action.Contains("UPDATE") && entityType.Contains("User", StringComparison.OrdinalIgnoreCase)))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "I. ACCOUNT MANAGEMENT & ACCESS CONTROL",
                UseCaseTitle = "5. UC_35.1: Update Admin Profile",
                RelatedTable = "Users",
                ActionCode = "UPDATE_PROFILE",
                EntityType = "Users",
                EntityId = entityId,
                Description = $"{actor} modified administrative profile details. Account data updated successfully.",
                DetailType = LogDetailType.Change
            };
        }
        // 7. UC_29.1: Approve / Reject Coach Application
        else if (action.Contains("COACH_APPLICATION") || action.Contains("COACH_APPLY") || ((action.Contains("APPROVE") || action.Contains("REJECT")) && entityType.Contains("CoachProfile", StringComparison.OrdinalIgnoreCase)))
        {
            var isReject = action.Contains("REJECT");
            info = new SystemLogUseCaseInfo
            {
                GroupName = "II. COACH & CERTIFICATE VERIFICATION",
                UseCaseTitle = "7. UC_29.1: Approve / Reject Coach Application",
                RelatedTable = "CoachProfiles (ApprovalStatus, ApprovedBy), Users",
                ActionCode = isReject ? "REJECT_COACH_APPLICATION" : "APPROVE_COACH_APPLICATION",
                EntityType = "CoachProfiles",
                EntityId = entityId,
                Description = $"{actor} processed review decision on Coach application (CoachID: {entityId}). Verification state updated.",
                DetailType = LogDetailType.Change
            };
        }
        // 8. UC_41.1.1: Approve / Reject Coach Certificate
        else if (action.Contains("CERTIFICATE") || entityType.Contains("Certificate", StringComparison.OrdinalIgnoreCase))
        {
            var isReject = action.Contains("REJECT");
            info = new SystemLogUseCaseInfo
            {
                GroupName = "II. COACH & CERTIFICATE VERIFICATION",
                UseCaseTitle = "8. UC_41.1.1: Approve / Reject Coach Certificate",
                RelatedTable = "CoachCertificates (VerificationStatus, VerifiedBy, VerifiedAt, RejectedReason)",
                ActionCode = isReject ? "REJECT_COACH_CERTIFICATE" : "APPROVE_COACH_CERTIFICATE",
                EntityType = "CoachCertificates",
                EntityId = entityId,
                Description = $"{actor} completed evaluation of Coach Certificate (CertificateID: {entityId}). Verification status updated.",
                DetailType = LogDetailType.Change
            };
        }
        // 9. UC_23.1: Process Coach Payout
        else if (action.Contains("PAYOUT") || entityType.Contains("Payout", StringComparison.OrdinalIgnoreCase))
        {
            var netAmount = GetProp("netpayoutamount", GetProp("amount", ""));
            var month = GetProp("payoutmonth", GetProp("month", DateTime.Now.Month.ToString()));
            var year = GetProp("payoutyear", GetProp("year", DateTime.Now.Year.ToString()));
            var coach = GetProp("coachname", GetProp("coach", "Coach"));
            var tx = GetProp("transactionref", GetProp("ref", entityId));
            var amountText = !string.IsNullOrWhiteSpace(netAmount) ? $"{netAmount} VND" : "funds";

            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "9. UC_23.1: Process Coach Payout",
                RelatedTable = "Payouts (NetPayoutAmount, PayoutMonth, PayoutYear, TransactionRef, ProcessedBy)",
                ActionCode = "PROCESS_COACH_PAYOUT",
                EntityType = "Payouts",
                EntityId = entityId,
                Description = $"{actor} finalized payout of {amountText} for period {month}/{year} to Coach {coach} (Tx Ref: {tx}). Payout status updated.",
                DetailType = LogDetailType.Change
            };
        }
        // 10. UC_33.1: Create Coach Subscription Plans
        else if ((action.Contains("CREATE") || action.Contains("ADD")) && (action.Contains("SUBSCRIPTION") || action.Contains("PLAN") || entityType.Contains("Subscription", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Plan", StringComparison.OrdinalIgnoreCase)))
        {
            var duration = GetProp("subscriptionduration", GetProp("duration", "30"));
            var amount = GetProp("amount", GetProp("price", ""));
            var currency = GetProp("currency", "VND");
            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "10. UC_33.1: Create Coach Subscription Plans",
                RelatedTable = "CoachSubscriptionPlans (Amount, Currency, SubscriptionDuration)",
                ActionCode = "CREATE_SUBSCRIPTION_PLAN",
                EntityType = "CoachSubscriptionPlans",
                EntityId = entityId,
                Description = $"{actor} created new Coach subscription plan for {duration} days at {amount} {currency}.",
                DetailType = LogDetailType.Initial
            };
        }
        // 12. UC_33.3: Deactivate Coach Subscription Plan
        else if ((action.Contains("DEACTIVATE") || action.Contains("DELETE")) && (action.Contains("SUBSCRIPTION") || action.Contains("PLAN") || entityType.Contains("Subscription", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Plan", StringComparison.OrdinalIgnoreCase)))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "12. UC_33.3: Deactivate Coach Subscription Plan",
                RelatedTable = "CoachSubscriptionPlans (IsActive = FALSE)",
                ActionCode = "DEACTIVATE_SUBSCRIPTION_PLAN",
                EntityType = "CoachSubscriptionPlans",
                EntityId = entityId,
                Description = $"{actor} modified publication status of Coach subscription plan (ID: {entityId}) to deactivated.",
                DetailType = LogDetailType.Change
            };
        }
        // 11. UC_33.2: Edit Coach Subscription Plan
        else if ((action.Contains("UPDATE") || action.Contains("EDIT") || action.Contains("PRICE")) && (action.Contains("SUBSCRIPTION") || action.Contains("PLAN") || entityType.Contains("Subscription", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Plan", StringComparison.OrdinalIgnoreCase)))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "11. UC_33.2: Edit Coach Subscription Plan",
                RelatedTable = "CoachSubscriptionPlans (CoachSubscriptionPlansID, Amount, Description)",
                ActionCode = "UPDATE_SUBSCRIPTION_PLAN",
                EntityType = "CoachSubscriptionPlans",
                EntityId = entityId,
                Description = $"{actor} updated Coach subscription plan parameters (ID: {entityId}). Configuration was replaced with new values.",
                DetailType = LogDetailType.Change
            };
        }
        // 13. UC_36.1 & UC_36.4: Activate / Deactivate Payment Account
        else if ((action.Contains("ACTIVATE") || action.Contains("DEACTIVATE") || action.Contains("TOGGLE")) && (action.Contains("PAYMENT") || action.Contains("GATEWAY") || entityType.Contains("Payment", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Gateway", StringComparison.OrdinalIgnoreCase)))
        {
            var isDeactivate = action.Contains("DEACTIVATE");
            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "13. UC_36.1 & UC_36.4: Activate / Deactivate Payment Account",
                RelatedTable = "PaymentGatewayConfigs (GatewayName, IsActive, UpdatedBy)",
                ActionCode = isDeactivate ? "DEACTIVATE_PAYMENT_GATEWAY" : "ACTIVATE_PAYMENT_GATEWAY",
                EntityType = "PaymentGatewayConfigs",
                EntityId = entityId,
                Description = $"{actor} toggled active operational state of payment gateway (GatewayID: {entityId}).",
                DetailType = LogDetailType.Change
            };
        }
        // 14. UC_36.2: Add Payment Account
        else if ((action.Contains("CREATE") || action.Contains("ADD")) && (action.Contains("PAYMENT") || action.Contains("GATEWAY") || entityType.Contains("Payment", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Gateway", StringComparison.OrdinalIgnoreCase)))
        {
            var gwName = GetProp("gatewayname", GetProp("name", "Payment Gateway"));
            var client = GetProp("clientid", GetProp("client", entityId));
            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "14. UC_36.2: Add Payment Account",
                RelatedTable = "PaymentGatewayConfigs (GatewayName, ClientID, UpdatedBy)",
                ActionCode = "CREATE_PAYMENT_GATEWAY",
                EntityType = "PaymentGatewayConfigs",
                EntityId = entityId,
                Description = $"{actor} configured new payment gateway: '{gwName}' (ClientID: {client}).",
                DetailType = LogDetailType.Initial
            };
        }
        // 15. UC_36.3: Edit Payment Account
        else if ((action.Contains("UPDATE") || action.Contains("EDIT")) && (action.Contains("PAYMENT") || action.Contains("GATEWAY") || entityType.Contains("Payment", StringComparison.OrdinalIgnoreCase) || entityType.Contains("Gateway", StringComparison.OrdinalIgnoreCase)))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "15. UC_36.3: Edit Payment Account",
                RelatedTable = "PaymentGatewayConfigs (GatewayName, WebhookUrl, UpdatedBy)",
                ActionCode = "UPDATE_PAYMENT_GATEWAY",
                EntityType = "PaymentGatewayConfigs",
                EntityId = entityId,
                Description = $"{actor} adjusted connection and webhook parameters for payment gateway (GatewayID: {entityId}).",
                DetailType = LogDetailType.Change
            };
        }
        // 16. UC_39: Process Refund Requests
        else if (action.Contains("REFUND") || entityType.Contains("Refund", StringComparison.OrdinalIgnoreCase))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "III. FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS",
                UseCaseTitle = "16. UC_39: Process Refund Requests",
                RelatedTable = "RefundRequests (RefundRequestID, ApprovedAmount, Status, StaffNote, ReviewedBy)",
                ActionCode = "PROCESS_REFUND_REQUEST",
                EntityType = "RefundRequests",
                EntityId = entityId,
                Description = $"{actor} processed decision for refund request #{entityId}. Status transitioned from pending to resolved.",
                DetailType = LogDetailType.Change
            };
        }
        // 17. UC_31.1: Create Location
        else if ((action.Contains("CREATE") || action.Contains("ADD")) && (action.Contains("LOCATION") || entityType.Contains("Location", StringComparison.OrdinalIgnoreCase)))
        {
            var locName = GetProp("locationname", GetProp("name", "New Branch"));
            var addr = GetProp("address", "");
            info = new SystemLogUseCaseInfo
            {
                GroupName = "IV. OPERATIONS, REPORTS & LOCATIONS",
                UseCaseTitle = "17. UC_31.1: Create Location",
                RelatedTable = "Locations (LocationName, Address)",
                ActionCode = "CREATE_LOCATION",
                EntityType = "Locations",
                EntityId = entityId,
                Description = $"{actor} added new gym/branch location: '{locName}' ({addr}).",
                DetailType = LogDetailType.Initial
            };
        }
        // 18. UC_31.2: Edit Location
        else if ((action.Contains("UPDATE") || action.Contains("EDIT")) && (action.Contains("LOCATION") || entityType.Contains("Location", StringComparison.OrdinalIgnoreCase)))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "IV. OPERATIONS, REPORTS & LOCATIONS",
                UseCaseTitle = "18. UC_31.2: Edit Location",
                RelatedTable = "Locations (LocationID, LocationName, Address)",
                ActionCode = "UPDATE_LOCATION",
                EntityType = "Locations",
                EntityId = entityId,
                Description = $"{actor} updated gym/branch location details (ID: {entityId}). Modifications saved.",
                DetailType = LogDetailType.Change
            };
        }
        // 19. UC_31.3: Delete Location
        else if ((action.Contains("DELETE") || action.Contains("REMOVE")) && (action.Contains("LOCATION") || entityType.Contains("Location", StringComparison.OrdinalIgnoreCase)))
        {
            var locName = GetProp("locationname", GetProp("name", "Location"));
            info = new SystemLogUseCaseInfo
            {
                GroupName = "IV. OPERATIONS, REPORTS & LOCATIONS",
                UseCaseTitle = "19. UC_31.3: Delete Location",
                RelatedTable = "Locations (LocationID, LocationName)",
                ActionCode = "DELETE_LOCATION",
                EntityType = "Locations",
                EntityId = entityId,
                Description = $"{actor} deleted gym/branch location '{locName}' from active system locations.",
                DetailType = LogDetailType.BeforeDelete
            };
        }
        // 21. UC_42.1: Process Appeal
        else if (action.Contains("APPEAL") || entityType.Contains("Appeal", StringComparison.OrdinalIgnoreCase))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "IV. OPERATIONS, REPORTS & LOCATIONS",
                UseCaseTitle = "21. UC_42.1: Process Appeal",
                RelatedTable = "Reports (ReportID, AppealStatus, AppealReviewNote, AppealReviewedBy, AppealReviewedAt)",
                ActionCode = "REVIEW_APPEAL",
                EntityType = "Reports",
                EntityId = entityId,
                Description = $"{actor} reviewed and recorded decision on user appeal (ReportID: {entityId}).",
                DetailType = LogDetailType.Change
            };
        }
        // 20. UC_32.1: Process Violation Report
        else if (action.Contains("VIOLATION") || action.Contains("REPORT") || entityType.Contains("Report", StringComparison.OrdinalIgnoreCase))
        {
            info = new SystemLogUseCaseInfo
            {
                GroupName = "IV. OPERATIONS, REPORTS & LOCATIONS",
                UseCaseTitle = "20. UC_32.1: Process Violation Report",
                RelatedTable = "Reports (ReportID, Status, ResolvedBy, ResolvedAt)",
                ActionCode = "RESOLVE_VIOLATION_REPORT",
                EntityType = "Reports",
                EntityId = entityId,
                Description = $"{actor} resolved violation report #{entityId}. Resolution report was logged into system.",
                DetailType = LogDetailType.Change
            };
        }
        else
        {
            // Generic Fallback
            var isInit = !string.IsNullOrWhiteSpace(log.NewValue) && string.IsNullOrWhiteSpace(log.OldValue);
            var isDel = string.IsNullOrWhiteSpace(log.NewValue) && !string.IsNullOrWhiteSpace(log.OldValue);
            info = new SystemLogUseCaseInfo
            {
                GroupName = "V. SYSTEM RUNTIME & AUDIT LOGS",
                UseCaseTitle = $"System Runtime Event ({action})",
                RelatedTable = string.IsNullOrWhiteSpace(entityType) ? "System" : entityType,
                ActionCode = action,
                EntityType = string.IsNullOrWhiteSpace(entityType) ? "System" : entityType,
                EntityId = entityId,
                Description = !string.IsNullOrWhiteSpace(log.Message) ? log.Message : $"{actor} executed action {action} on {entityType}.",
                DetailType = isInit ? LogDetailType.Initial : (isDel ? LogDetailType.BeforeDelete : LogDetailType.Change)
            };
        }

        // Extract structured, human-readable field comparison
        info.FieldChanges = ExtractFieldChanges(log.OldValue, log.NewValue);

        return info;
    }

    public static List<LogFieldChangeItem> ExtractFieldChanges(string? oldJson, string? newJson)
    {
        var result = new List<LogFieldChangeItem>();
        var oldDict = ParseRawDict(oldJson);
        var newDict = ParseRawDict(newJson);

        // Collect distinct keys in order
        var keys = new List<string>();
        foreach (var k in newDict.Keys)
        {
            if (!keys.Contains(k, StringComparer.OrdinalIgnoreCase))
                keys.Add(k);
        }
        foreach (var k in oldDict.Keys)
        {
            if (!keys.Contains(k, StringComparer.OrdinalIgnoreCase))
                keys.Add(k);
        }

        foreach (var key in keys)
        {
            oldDict.TryGetValue(key, out var rawOld);
            newDict.TryGetValue(key, out var rawNew);

            var item = new LogFieldChangeItem
            {
                Key = key,
                DisplayName = GetDisplayName(key),
                RawOldValue = rawOld,
                RawNewValue = rawNew,
                FormattedOldValue = FormatValue(key, rawOld),
                FormattedNewValue = FormatValue(key, rawNew),
                IsStatus = key.IndexOf("status", StringComparison.OrdinalIgnoreCase) >= 0
            };

            item.IsChanged = !string.Equals(item.FormattedOldValue, item.FormattedNewValue, StringComparison.Ordinal)
                             && rawOld != null && rawNew != null;

            if (item.IsStatus)
            {
                item.OldStatusBadgeClass = GetStatusBadgeClass(rawOld);
                item.NewStatusBadgeClass = GetStatusBadgeClass(rawNew);
            }

            var textLen = Math.Max(rawOld?.Length ?? 0, rawNew?.Length ?? 0);
            item.IsLongText = textLen > 65 || (rawOld?.Contains('\n') == true) || (rawNew?.Contains('\n') == true)
                              || item.FormattedOldValue.Contains('\n') || item.FormattedNewValue.Contains('\n')
                              || IsJsonStructure(rawOld) || IsJsonStructure(rawNew);

            result.Add(item);
        }

        return result;
    }

    private static string GetDisplayName(string key)
    {
        if (FieldDisplayNames.TryGetValue(key, out var name))
            return name;

        // Auto format camelCase or snake_case to Title Case
        var spaced = Regex.Replace(key, "([a-z])([A-Z])", "$1 $2").Replace('_', ' ');
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced);
    }

    private static readonly (string Pattern, string Replacement)[] PhraseTranslations = new[]
    {
        // PayOS & Banking Gateway
        ("Idempotency key đã tồn tại, vui lòng dùng key khác", "Idempotency key already exists, please use a different key"),
        ("Idempotency key đã tồn tại", "Idempotency key already exists"),
        ("vui lòng dùng key khác", "please use a different key"),
        ("Tài khoản nguồn không đủ số dư", "Insufficient balance in source payout account"),
        ("Tài khoản không đủ số dư", "Insufficient account balance"),
        ("Số dư không đủ", "Insufficient balance"),
        ("Tài khoản thụ hưởng không hợp lệ", "Invalid beneficiary bank account"),
        ("Ngân hàng thụ hưởng không hợp lệ", "Invalid beneficiary bank"),
        ("Ngân hàng thụ hưởng đang bảo trì", "Beneficiary bank is under maintenance"),
        ("Ngân hàng không hỗ trợ", "Unsupported bank"),
        ("Tài khoản không hợp lệ", "Invalid account"),
        ("Từ chối chuyển tiền", "Transfer rejected"),
        ("Chuyển tiền bị từ chối", "Transfer rejected"),
        ("Giao dịch thất bại", "Transaction failed"),
        ("Giao dịch thành công", "Transaction successful"),
        ("Giao dịch bị từ chối", "Transaction rejected"),
        ("Giao dịch đã bị hủy", "Transaction has been cancelled"),
        ("Hết thời gian chờ", "Request timed out"),
        ("Vui lòng thử lại sau", "Please try again later"),
        ("Không tìm thấy giao dịch", "Transaction not found"),
        ("Không tìm thấy", "Not found"),
        ("Đã tồn tại", "Already exists"),
        ("Lỗi hệ thống", "System error"),
        ("Lỗi kết nối", "Connection error"),
        ("Chữ ký không hợp lệ", "Invalid signature"),

        // PayOS error codes
        ("E2: transfer rejected", "E2: Transfer rejected by banking gateway"),
        ("transfer rejected", "Transfer rejected"),
        ("E1: account not found", "E1: Account not found"),
        ("E3: invalid amount", "E3: Invalid amount"),

        // Refund policy reasons & notes
        ("Học viên chuyển chỗ ở", "Trainee relocated"),
        ("Lịch làm việc đổi sang ca đêm", "Work schedule changed to night shift"),
        ("Bận đi công tác đột xuất ở nước ngoài 2 tháng, không thể tiếp tục lịch tập 1-1.", "Emergency 2-month overseas business trip, unable to continue 1-on-1 training."),
        ("Bận đi công tác", "Busy with business trip"),
        ("Bị bong gân cổ chân", "Sprained ankle injury"),
        ("Mệt quá muốn hủy", "Trainee requested cancellation due to fatigue"),
        ("Từ chối vì học quá 50%", "Rejected: Trainee attended over 50% of training sessions"),
        ("Đã duyệt hoàn tiền 100%", "Approved 100% refund disbursement"),
        ("Đã duyệt hoàn tiền", "Approved refund request"),
        ("Từ chối hoàn tiền", "Rejected refund request"),
        ("Tài khoản bị khóa", "Account locked"),
        ("Tài khoản đã mở khóa", "Account unlocked")
    };

    public static string TranslateCommonPhrases(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;

        var result = input;
        foreach (var (pattern, replacement) in PhraseTranslations)
        {
            if (result.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result = Regex.Replace(result, Regex.Escape(pattern), replacement, RegexOptions.IgnoreCase);
            }
        }

        return result;
    }

    private static bool IsJsonStructure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        if ((t.StartsWith("{") && t.EndsWith("}")) || (t.StartsWith("[") && t.EndsWith("]")))
            return true;
        if (t.StartsWith("\"") && t.EndsWith("\"") && t.Length > 4)
        {
            var inner = t[1..^1].Trim();
            if ((inner.StartsWith("{") && inner.EndsWith("}")) || (inner.StartsWith("[") && inner.EndsWith("]")))
                return true;
        }
        return false;
    }

    public static string FormatJsonValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return "—";
        var trimmed = rawValue.Trim();

        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length > 2)
        {
            trimmed = trimmed[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var lines = new List<string>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var propName = GetDisplayName(prop.Name);
                    var valStr = FormatElementValue(prop.Name, prop.Value);
                    lines.Add($"• {propName}: {valStr}");
                }

                if (lines.Count > 0)
                {
                    return string.Join("\n", lines);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var items = new List<string>();
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    var valStr = FormatElementValue("item", elem);
                    items.Add($"• {valStr}");
                }
                if (items.Count > 0)
                {
                    return string.Join("\n", items);
                }
            }
        }
        catch
        {
            // If parsing fails, fall back to string cleaning
        }

        return TranslateCommonPhrases(trimmed);
    }

    private static string FormatElementValue(string key, JsonElement elem)
    {
        var keyLower = key.ToLowerInvariant();
        return elem.ValueKind switch
        {
            JsonValueKind.String => TranslateCommonPhrases(elem.GetString() ?? string.Empty),
            JsonValueKind.Number => (keyLower.Contains("amount") || keyLower.Contains("price")) && elem.TryGetDecimal(out var d)
                ? $"{d:N0} VND"
                : elem.ToString(),
            JsonValueKind.True => "Yes (True)",
            JsonValueKind.False => "No (False)",
            JsonValueKind.Null => "—",
            JsonValueKind.Object => FormatJsonValue(elem.GetRawText()),
            JsonValueKind.Array => string.Join(", ", elem.EnumerateArray().Select(x => FormatElementValue(key, x))),
            _ => TranslateCommonPhrases(elem.ToString())
        };
    }

    private static string FormatValue(string key, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return "—";

        var trimmed = rawValue.Trim();
        var keyLower = key.ToLowerInvariant();

        // 0. If value is JSON object or array, parse and format as human-readable language
        if (IsJsonStructure(trimmed))
        {
            return FormatJsonValue(trimmed);
        }

        // 1. Boolean formats
        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            if (keyLower.Contains("lock")) return "Locked";
            if (keyLower.Contains("active")) return "Active";
            return "Yes (True)";
        }
        if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
        {
            if (keyLower.Contains("lock")) return "Unlocked";
            if (keyLower.Contains("active")) return "Inactive";
            return "No (False)";
        }

        // 2. Numeric currency amount formats
        if ((keyLower.Contains("amount") || keyLower.Contains("price") || keyLower.Contains("portion") || keyLower.Contains("paid"))
            && decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
        {
            return $"{dec:N0} VND";
        }

        // 3. Date / Time formats (ISO string)
        if ((keyLower.Contains("at") || keyLower.Contains("date") || keyLower.Contains("time") || trimmed.Contains("T"))
            && DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt)
            && dt.Year > 2000)
        {
            return dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        }

        // 4. Duration
        if (keyLower.Contains("duration") && int.TryParse(trimmed, out var duration))
        {
            return $"{duration} days";
        }

        // 5. Month / Year
        if (keyLower.EndsWith("month") && int.TryParse(trimmed, out var m))
        {
            return $"Month {m}";
        }
        if (keyLower.EndsWith("year") && int.TryParse(trimmed, out var y))
        {
            return $"Year {y}";
        }

        return TranslateCommonPhrases(trimmed);
    }

    public static string GetStatusBadgeClass(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "status-default";
        var s = status.Trim().ToUpperInvariant();

        if (s.Contains("APPROV") || s.Contains("ACTIVE") || s.Contains("VERIF") || s.Contains("RESOLV") || s.Contains("COMPLET") || s.Contains("SUCCESS"))
            return "status-success";

        if (s.Contains("PENDING") || s.Contains("WAIT") || s.Contains("REVIEW") || s.Contains("SUBMIT") || s.Contains("PARTIAL"))
            return "status-warning";

        if (s.Contains("REJECT") || s.Contains("LOCK") || s.Contains("DEACTIV") || s.Contains("FAIL") || s.Contains("CANCEL") || s.Contains("DISMISS"))
            return "status-danger";

        return "status-default";
    }

    private static Dictionary<string, string> ParseRawDict(string? json)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return dict;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ToString();
                }
            }
        }
        catch { }
        return dict;
    }

    private static (Dictionary<string, string> newProps, Dictionary<string, string> oldProps) ParseProps(string? newJson, string? oldJson)
    {
        return (ParseRawDict(newJson), ParseRawDict(oldJson));
    }

    public static string GeneratePlainText(SystemLogUseCaseInfo info, string? oldValue, string? newValue, Func<string?, string> formatJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine(info.GroupName);
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine(info.UseCaseTitle);
        sb.AppendLine($"   - Related Table: {info.RelatedTable}");
        sb.AppendLine($"   - Action Code: {info.ActionCode}");
        sb.AppendLine($"   - Entity Type: {info.EntityType}");
        sb.AppendLine($"   - Entity ID: {(string.IsNullOrWhiteSpace(info.EntityId) ? "N/A" : info.EntityId)}");
        sb.AppendLine("   - Description:");
        sb.AppendLine($"     \"{info.Description}\"");

        if (info.DetailType == LogDetailType.Initial)
        {
            sb.AppendLine("   - Details:");
            sb.AppendLine("     + Initial Value:");
            foreach (var f in info.FieldChanges)
            {
                if (f.FormattedNewValue.Contains('\n'))
                {
                    sb.AppendLine($"       * {f.DisplayName} ({f.Key}):");
                    foreach (var sub in f.FormattedNewValue.Split('\n'))
                    {
                        sb.AppendLine($"         {sub}");
                    }
                }
                else
                {
                    sb.AppendLine($"       * {f.DisplayName} ({f.Key}): {f.FormattedNewValue}");
                }
            }
        }
        else if (info.DetailType == LogDetailType.BeforeDelete)
        {
            sb.AppendLine("   - Details:");
            sb.AppendLine("     + Value Before Deletion:");
            foreach (var f in info.FieldChanges)
            {
                if (f.FormattedOldValue.Contains('\n'))
                {
                    sb.AppendLine($"       * {f.DisplayName} ({f.Key}):");
                    foreach (var sub in f.FormattedOldValue.Split('\n'))
                    {
                        sb.AppendLine($"         {sub}");
                    }
                }
                else
                {
                    sb.AppendLine($"       * {f.DisplayName} ({f.Key}): {f.FormattedOldValue}");
                }
            }
        }
        else
        {
            sb.AppendLine("   - Changes Summary:");
            if (info.FieldChanges.Any(f => f.RawOldValue != null))
            {
                sb.AppendLine("     + Previous Value:");
                foreach (var f in info.FieldChanges.Where(f => f.RawOldValue != null))
                {
                    if (f.FormattedOldValue.Contains('\n'))
                    {
                        sb.AppendLine($"       * {f.DisplayName} ({f.Key}):");
                        foreach (var sub in f.FormattedOldValue.Split('\n'))
                        {
                            sb.AppendLine($"         {sub}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"       * {f.DisplayName} ({f.Key}): {f.FormattedOldValue}");
                    }
                }
            }
            if (info.FieldChanges.Any(f => f.RawNewValue != null))
            {
                sb.AppendLine("     + Current Value:");
                foreach (var f in info.FieldChanges.Where(f => f.RawNewValue != null))
                {
                    if (f.FormattedNewValue.Contains('\n'))
                    {
                        sb.AppendLine($"       * {f.DisplayName} ({f.Key}):");
                        foreach (var sub in f.FormattedNewValue.Split('\n'))
                        {
                            sb.AppendLine($"         {sub}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"       * {f.DisplayName} ({f.Key}): {f.FormattedNewValue}");
                    }
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}
