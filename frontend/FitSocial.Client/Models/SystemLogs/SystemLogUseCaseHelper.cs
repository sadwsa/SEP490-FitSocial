using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FitSocial.Client.Models.SystemLogs;

public enum LogDetailType
{
    Initial,      // CREATE: Before = —, After = Created values
    BeforeDelete, // DELETE: Before = Deleted values, After = —
    Change        // UPDATE / LOCK / UNLOCK / APPROVE / REJECT: Before = Old value, After = New value
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
    public string ActionType { get; set; } = "UPDATE";
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string TargetDisplayName { get; set; } = "—";
    public string Description { get; set; } = string.Empty;
    public LogDetailType DetailType { get; set; } = LogDetailType.Change;

    public List<LogFieldChangeItem> FieldChanges { get; set; } = new();
}

public static class SystemLogUseCaseHelper
{
    private static readonly Dictionary<string, string> FieldDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["status"] = "Status",
        ["approvalstatus"] = "Approval Status",
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
        ["plandescription"] = "Plan Description",
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
        ["phone"] = "Phone Number",
        ["gender"] = "Gender",
        ["dateofbirth"] = "Date of Birth",
        ["avatarurl"] = "Avatar URL",
        ["avatar"] = "Avatar URL",
        ["islocked"] = "Lock Status",
        ["isactive"] = "Active Status",
        ["isdefault"] = "Default Gateway",
        ["warningcount"] = "Warning Count",
        ["amount"] = "Amount",
        ["currency"] = "Currency",
        ["subscriptionduration"] = "Subscription Duration",
        ["trainingpackageduration"] = "Training Package Duration",
        ["price"] = "Price",
        ["gatewayname"] = "Gateway Name",
        ["code"] = "Gateway Code",
        ["clientid"] = "Client ID",
        ["webhookurl"] = "Webhook URL",
        ["locationname"] = "Location Name",
        ["address"] = "Address",
        ["certificatename"] = "Certificate Name",
        ["verificationstatus"] = "Verification Status",
        ["verifiedby"] = "Verified By",
        ["verifiedat"] = "Verified At",
        ["rejectedreason"] = "Rejection Reason",
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
        ["payoutstatus"] = "Payout Status",
        ["approvedby"] = "Approved By",
        ["resolvedby"] = "Resolved By",
        ["lockedby"] = "Locked By",
        ["rejectionreason"] = "Rejection Reason",
        ["note"] = "Review Note"
    };

    private static readonly Regex RawGuidBracketRegex = new(
        @"\[[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
        RegexOptions.Compiled);

    private static readonly Regex RawGuidParenRegex = new(
        @"\([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\)",
        RegexOptions.Compiled);

    private static readonly Regex RawGuidRegex = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);

    private static readonly Regex SystemBracketNoteRegex = new(
        @"\[(?:Policy\s*:|Automatic bank transfer|POLICY_SNAPSHOT)[^\]]*\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string ExtractCleanStaffNote(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return SystemBracketNoteRegex.Replace(raw, string.Empty).Trim();
    }

    private static readonly HashSet<string> TargetContextOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "targetName", "targetEmail", "targetRole",
        "userId", "coachId", "certificateId", "gatewayId", "planId", "locationId", "refundRequestId", "payoutId", "reportId"
    };

    private static readonly HashSet<string> RedundantAuditActorMetaKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "processedBy", "processedAt", "verifiedBy", "verifiedAt",
        "approvedBy", "rejectedBy", "resolvedBy", "reviewedBy", "reviewedAt", "lockedBy"
    };

    private static readonly string[] InvariantEntityContextKeys =
    {
        "buyer", "seller", "package", "order", "originalPaidAmount",
        "requestedAmount", "coachPayoutPortion", "policyCase", "bankAccount",
        "Month", "Year", "TotalNetAmount", "ItemsCount", "priceId", "currency"
    };

    private static readonly string[] DisplayNameCandidateKeys =
    {
        "buyer", "targetName", "fullName", "coachName", "locationName",
        "planName", "planDescription", "gatewayName", "certificateName",
        "seller", "package", "title", "name", "targetEmail", "email"
    };

    public static string GetCanonicalActionType(SystemLogItemModel log)
    {
        if (!string.IsNullOrWhiteSpace(log.ActionType))
        {
            return log.ActionType.Trim().ToUpperInvariant();
        }

        var a = (log.Action ?? string.Empty).Trim().ToUpperInvariant();
        if (a.Contains("CREATE") || a.Contains("ADD")) return "CREATE";
        if (a.Contains("DELETE") || a.Contains("REMOVE")) return "DELETE";
        if (a.Contains("UNLOCK")) return "UNLOCK";
        if (a.Contains("LOCK")) return "LOCK";
        if (a.Contains("APPROVE")) return "APPROVE";
        if (a.Contains("REJECT") || a.Contains("DISMISS")) return "REJECT";
        return "UPDATE";
    }

    public static string GetHumanReadableActionTitle(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return "Administrative Activity";
        var a = action.Trim().ToUpperInvariant();
        return a switch
        {
            "CREATE_STAFF" => "Create Staff Account",
            "UPDATE_STAFF" => "Edit Staff Account",
            "LOCK_USER" or "LOCK_ACCOUNT" or "LOCK" => "Lock User Account",
            "UNLOCK_USER" or "UNLOCK_ACCOUNT" or "UNLOCK" => "Unlock User Account",
            "AUTO_LOCK_USER_VIOLATION" => "Auto-Lock Account (Violation)",
            "WARN_USER" or "WARN" => "Issue Violation Warning",
            "BLOCK" => "Block User Account",
            "UPDATE_PROFILE" => "Update Own Profile",
            "CHANGE_PASSWORD" => "Change Password",
            "CREATE_LOCATION" => "Create Location",
            "UPDATE_LOCATION" => "Edit Location",
            "DELETE_LOCATION" => "Delete Location",
            "APPROVE_COACH_APPLICATION" => "Approve Coach Application",
            "REJECT_COACH_APPLICATION" => "Reject Coach Application",
            "APPROVE_CERTIFICATE" or "APPROVE_COACH_CERTIFICATE" => "Approve Coach Certificate",
            "REJECT_CERTIFICATE" or "REJECT_COACH_CERTIFICATE" => "Reject Coach Certificate",
            "CREATE_COACH_SUBSCRIPTION_PLAN" or "CREATE_SUBSCRIPTION_PLAN" => "Create Subscription Plan",
            "UPDATE_COACH_SUBSCRIPTION_PLAN" or "UPDATE_SUBSCRIPTION_PLAN" or "UPDATE_PRICE" => "Edit Subscription Plan",
            "DELETE_COACH_SUBSCRIPTION_PLAN" or "DELETE_SUBSCRIPTION_PLAN" => "Delete Subscription Plan",
            "CREATE_PAYMENT_GATEWAY" or "ADD_PAYMENT_ACCOUNT" => "Create Payment Gateway",
            "UPDATE_PAYMENT_GATEWAY" or "EDIT_PAYMENT_ACCOUNT" => "Edit Payment Gateway",
            "TOGGLE_PAYMENT_GATEWAY" => "Toggle Payment Gateway",
            "ACTIVATE_PAYMENT_ACCOUNT" => "Activate Payment Gateway",
            "DEACTIVATE_PAYMENT_ACCOUNT" => "Deactivate Payment Gateway",
            "SET_DEFAULT_PAYMENT_GATEWAY" => "Set Default Gateway",
            "DELETE_PAYMENT_GATEWAY" or "DELETE_PAYMENT_ACCOUNT" => "Delete Payment Gateway",
            "CREATE_REFUND_REQUEST" => "Create Refund Request",
            "APPROVE_REFUND" or "APPROVE_REFUND_REQUEST" => "Approve Refund Request",
            "REJECT_REFUND" or "REJECT_REFUND_REQUEST" => "Reject Refund Request",
            "PROCESS_PAYOUT" or "PAYOUT_PROCESSED" => "Process Coach Payout",
            "PAYOUT_PARTIAL" => "Process Partial Payout",
            "PAYOUT_FAILED" => "Failed Coach Payout",
            "PAYOUT_BLOCKED" => "Blocked Coach Payout",
            "PAYOUT_SUBMITTED" => "Submit Coach Payout",
            "PAYOUT_NOOP" => "Check Coach Payout",
            "GENERATE_COACH_PAYOUT" => "Generate Monthly Payouts",
            "DELETE_POST" => "Delete Violation Post",
            "RESOLVE_REPORT" => "Resolve Violation Report",
            "DISMISS_REPORT" => "Dismiss Violation Report",
            "REJECT_REPORT" => "Reject Violation Report",
            "APPROVE_APPEAL_UNLOCK_USER" => "Approve Appeal & Unlock",
            "REJECT_APPEAL" => "Reject Violation Appeal",
            "SYSTEM_ERROR" => "System Runtime Error",
            "BUSINESS_WARNING" => "Validation Warning",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(a.Replace('_', ' ').ToLowerInvariant())
        };
    }

    public static string ResolveTargetDisplayName(SystemLogItemModel log)
    {
        var (newProps, oldProps) = ParseProps(log.NewValue, log.OldValue);
        var entityType = (log.Source ?? string.Empty).Trim();

        // 1. RefundRequest: take the requester/buyer name directly (e.g. "Nguyễn Văn Sang") without order code
        var buyer = GetProp(newProps, oldProps, "buyer");
        if (!string.IsNullOrWhiteSpace(buyer))
        {
            return CleanDisplayName(buyer);
        }

        // 2. User / Staff / CoachProfile: take fullName or targetName without email/code in parentheses
        var fullName = GetProp(newProps, oldProps, "fullName") ?? GetProp(newProps, oldProps, "targetName");
        if (!string.IsNullOrWhiteSpace(fullName) && !Guid.TryParse(fullName, out _))
        {
            return CleanDisplayName(fullName);
        }

        // 3. CoachCertificate / Payout: take coachName or certificateName without codes
        var coachName = GetProp(newProps, oldProps, "coachName");
        if (!string.IsNullOrWhiteSpace(coachName) && !Guid.TryParse(coachName, out _))
        {
            return CleanDisplayName(coachName);
        }

        var certName = GetProp(newProps, oldProps, "certificateName");
        if (!string.IsNullOrWhiteSpace(certName) && !Guid.TryParse(certName, out _))
        {
            return CleanDisplayName(certName);
        }

        // 4. Location / Gateway / Subscription Plan
        var locationName = GetProp(newProps, oldProps, "locationName");
        if (!string.IsNullOrWhiteSpace(locationName))
        {
            return CleanDisplayName(locationName);
        }

        var gatewayName = GetProp(newProps, oldProps, "gatewayName");
        if (!string.IsNullOrWhiteSpace(gatewayName))
        {
            return CleanDisplayName(gatewayName);
        }

        var planDesc = GetProp(newProps, oldProps, "planDescription") ?? GetProp(newProps, oldProps, "planName");
        if (!string.IsNullOrWhiteSpace(planDesc))
        {
            return CleanDisplayName(planDesc);
        }

        if (entityType.Equals("CoachSubscriptionPlan", StringComparison.OrdinalIgnoreCase))
        {
            var desc = GetProp(newProps, oldProps, "description");
            if (!string.IsNullOrWhiteSpace(desc) &&
                !desc.StartsWith("Updated ", StringComparison.OrdinalIgnoreCase) &&
                !desc.StartsWith("Created ", StringComparison.OrdinalIgnoreCase) &&
                !desc.StartsWith("Deleted ", StringComparison.OrdinalIgnoreCase) &&
                !desc.StartsWith("Deactivated ", StringComparison.OrdinalIgnoreCase))
            {
                return CleanDisplayName(desc);
            }
        }

        // 5. Candidate keys
        foreach (var candidate in DisplayNameCandidateKeys)
        {
            var val = GetProp(newProps, oldProps, candidate);
            if (!string.IsNullOrWhiteSpace(val) && !Guid.TryParse(val, out _) && !val.StartsWith("#ORD-", StringComparison.OrdinalIgnoreCase))
            {
                return CleanDisplayName(val);
            }
        }

        // 6. Check backend-supplied TargetDisplayName (cleaned of any codes/parentheses)
        if (!string.IsNullOrWhiteSpace(log.TargetDisplayName) && !Guid.TryParse(log.TargetDisplayName.Trim(), out _))
        {
            return CleanDisplayName(log.TargetDisplayName.Trim());
        }

        if (string.Equals(log.Action, "UPDATE_PROFILE", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(log.ActorName))
        {
            return CleanDisplayName(log.ActorName);
        }

        return "—";
    }

    private static string CleanDisplayName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        var s = raw.Trim();

        // If formatted as "pkg (#ORD-XXXX - Buyer Name)", extract "Buyer Name"
        var ordDashIdx = s.IndexOf("#ORD-", StringComparison.OrdinalIgnoreCase);
        if (ordDashIdx >= 0)
        {
            var dashAfterOrd = s.IndexOf(" - ", ordDashIdx, StringComparison.Ordinal);
            var closeParen = s.LastIndexOf(')');
            if (dashAfterOrd > 0 && closeParen > dashAfterOrd)
            {
                var extractedBuyer = s.Substring(dashAfterOrd + 3, closeParen - (dashAfterOrd + 3)).Trim();
                if (!string.IsNullOrWhiteSpace(extractedBuyer))
                {
                    return extractedBuyer;
                }
            }
        }

        // Strip trailing parenthesized code/email/cycle such as "Name (email@domain.com)" or "Name (11/2026)"
        var openParen = s.IndexOf('(');
        if (openParen > 0 && s.EndsWith(")", StringComparison.Ordinal))
        {
            var beforeParen = s[..openParen].Trim();
            if (!string.IsNullOrWhiteSpace(beforeParen))
            {
                return beforeParen;
            }
        }

        return s;
    }

    private static string? GetProp(Dictionary<string, string> primary, Dictionary<string, string> secondary, string key)
    {
        if (primary.TryGetValue(key, out var v1) && !string.IsNullOrWhiteSpace(v1))
            return v1.Trim();
        if (secondary.TryGetValue(key, out var v2) && !string.IsNullOrWhiteSpace(v2))
            return v2.Trim();
        return null;
    }

    public static string FormatReadableDescription(SystemLogItemModel log)
    {
        var targetDisplayName = ResolveTargetDisplayName(log);
        var actor = !string.IsNullOrWhiteSpace(log.ActorName) ? log.ActorName.Trim() : "Administrator";
        var action = (log.Action ?? string.Empty).Trim().ToUpperInvariant();
        var entityType = (log.Source ?? "Entity").Trim();
        var friendlyEntity = FormatFriendlyEntityType(entityType);
        var targetLabel = targetDisplayName != "—" ? targetDisplayName : friendlyEntity;

        var generated = GenerateActionDescription(action, targetLabel, targetDisplayName, friendlyEntity, log.EntityId, actor);
        if (!string.IsNullOrWhiteSpace(generated))
        {
            return generated;
        }

        var msg = log.Message?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(msg))
        {
            var replacement = targetDisplayName != "—" ? targetDisplayName : friendlyEntity;
            msg = RawGuidBracketRegex.Replace(msg, replacement);
            msg = RawGuidParenRegex.Replace(msg, replacement);
            msg = RawGuidRegex.Replace(msg, replacement);
            return msg;
        }

        return $"{GetHumanReadableActionTitle(action)} on {friendlyEntity} by {actor}";
    }

    private static string GenerateActionDescription(
        string action,
        string targetLabel,
        string targetDisplayName,
        string friendlyEntity,
        string? entityId,
        string actor)
    {
        var isGuidEntity = Guid.TryParse(entityId, out _);

        return action switch
        {
            "CREATE_STAFF" => $"Created staff account for {targetLabel} by {actor}",
            "UPDATE_STAFF" => $"Updated staff account details for {targetLabel} by {actor}",
            "LOCK_USER" or "LOCK_ACCOUNT" or "LOCK" => $"Locked user account {targetLabel} by {actor}",
            "UNLOCK_USER" or "UNLOCK_ACCOUNT" or "UNLOCK" => $"Unlocked user account {targetLabel} by {actor}",
            "WARN_USER" or "WARN" => $"Issued violation warning to {targetLabel} by {actor}",
            "BLOCK" => $"Blocked user account {targetLabel} by {actor}",
            "AUTO_LOCK_USER_VIOLATION" => $"Automatically locked user account {targetLabel} after reaching violation threshold",
            "UPDATE_PROFILE" => $"Updated own administrative profile by {actor}",
            "CREATE_LOCATION" => $"Created training location {targetLabel} by {actor}",
            "UPDATE_LOCATION" => $"Updated training location {targetLabel} by {actor}",
            "DELETE_LOCATION" => $"Deleted training location {targetLabel} by {actor}",
            "APPROVE_COACH_APPLICATION" => $"Approved coach application for {targetLabel} by {actor}",
            "REJECT_COACH_APPLICATION" => $"Rejected coach application for {targetLabel} by {actor}",
            "APPROVE_CERTIFICATE" or "APPROVE_COACH_CERTIFICATE" => $"Approved coach certificate {targetLabel} by {actor}",
            "REJECT_CERTIFICATE" or "REJECT_COACH_CERTIFICATE" => $"Rejected coach certificate {targetLabel} by {actor}",
            "CREATE_COACH_SUBSCRIPTION_PLAN" or "CREATE_SUBSCRIPTION_PLAN" => $"Created subscription plan {targetLabel} by {actor}",
            "UPDATE_COACH_SUBSCRIPTION_PLAN" or "UPDATE_SUBSCRIPTION_PLAN" or "UPDATE_PRICE" => $"Updated subscription plan {targetLabel} by {actor}",
            "DELETE_COACH_SUBSCRIPTION_PLAN" or "DELETE_SUBSCRIPTION_PLAN" => $"Deleted subscription plan {targetLabel} by {actor}",
            "CREATE_PAYMENT_GATEWAY" or "ADD_PAYMENT_ACCOUNT" => $"Created payment gateway configuration {targetLabel} by {actor}",
            "UPDATE_PAYMENT_GATEWAY" or "EDIT_PAYMENT_ACCOUNT" => $"Updated payment gateway configuration {targetLabel} by {actor}",
            "TOGGLE_PAYMENT_GATEWAY" => $"Toggled payment gateway status for {targetLabel} by {actor}",
            "ACTIVATE_PAYMENT_ACCOUNT" => $"Activated payment gateway configuration {targetLabel} by {actor}",
            "DEACTIVATE_PAYMENT_ACCOUNT" => $"Deactivated payment gateway configuration {targetLabel} by {actor}",
            "SET_DEFAULT_PAYMENT_GATEWAY" => $"Set default payment gateway to {targetLabel} by {actor}",
            "DELETE_PAYMENT_GATEWAY" or "DELETE_PAYMENT_ACCOUNT" => $"Deleted payment gateway configuration {targetLabel} by {actor}",
            "CREATE_REFUND_REQUEST" => $"Submitted refund request for {targetLabel} by {actor}",
            "APPROVE_REFUND" or "APPROVE_REFUND_REQUEST" => $"Approved refund request for {targetLabel} by {actor}",
            "REJECT_REFUND" or "REJECT_REFUND_REQUEST" => $"Rejected refund request for {targetLabel} by {actor}",
            "PROCESS_PAYOUT" or "PAYOUT_PROCESSED" => $"Processed coach payout for {targetLabel} by {actor}",
            "PAYOUT_PARTIAL" => $"Processed partial coach payout for {targetLabel} by {actor}",
            "PAYOUT_FAILED" => $"Failed to process coach payout for {targetLabel} by {actor}",
            "PAYOUT_BLOCKED" => $"Blocked coach payout for {targetLabel} by {actor}",
            "PAYOUT_SUBMITTED" => $"Submitted coach payout transfer for {targetLabel} by {actor}",
            "PAYOUT_NOOP" => $"Checked already-paid coach payout for {targetLabel} by {actor}",
            "GENERATE_COACH_PAYOUT" => $"Generated monthly coach payout cycle by {actor}",
            "DELETE_POST" => $"Deleted reported post of {targetLabel} by {actor}",
            "RESOLVE_REPORT" => $"Resolved violation report for {targetLabel} by {actor}",
            "DISMISS_REPORT" => $"Dismissed violation report for {targetLabel} by {actor}",
            "REJECT_REPORT" => $"Rejected violation report for {targetLabel} by {actor}",
            "APPROVE_APPEAL_UNLOCK_USER" => $"Approved violation appeal and unlocked account {targetLabel} by {actor}",
            "REJECT_APPEAL" => $"Rejected violation appeal for {targetLabel} by {actor}",
            "SYSTEM_ERROR" => !string.IsNullOrWhiteSpace(entityId) && !isGuidEntity
                ? $"Unhandled runtime system error occurred on path: {entityId}"
                : "Unhandled runtime system error occurred",
            "BUSINESS_WARNING" => !string.IsNullOrWhiteSpace(entityId) && !isGuidEntity
                ? $"Business validation event on path: {entityId}"
                : "Business validation warning recorded",
            _ => targetDisplayName != "—"
                ? $"{GetHumanReadableActionTitle(action)} for {targetDisplayName} by {actor}"
                : $"{GetHumanReadableActionTitle(action)} on {friendlyEntity} by {actor}"
        };
    }

    private static string FormatFriendlyEntityType(string? entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType)) return "System";
        return entityType.Trim() switch
        {
            "CoachSubscriptionPlan" => "Subscription Plan",
            "RefundRequest" => "Refund Request",
            "CoachProfile" => "Coach Application",
            "CoachCertificate" => "Coach Certificate",
            "CoachUpgrade" => "Coach Upgrade",
            "PaymentGatewayConfig" => "Payment Gateway",
            _ => entityType.Trim()
        };
    }

    public static SystemLogUseCaseInfo Resolve(SystemLogItemModel log)
    {
        var action = (log.Action ?? string.Empty).Trim().ToUpperInvariant();
        var entityType = (log.Source ?? string.Empty).Trim();
        var entityId = (log.EntityId ?? string.Empty).Trim();
        var actor = !string.IsNullOrWhiteSpace(log.ActorName) ? log.ActorName : "Staff/Admin";
        var canonicalActionType = GetCanonicalActionType(log);
        var targetDisplayName = ResolveTargetDisplayName(log);

        var detailType = canonicalActionType switch
        {
            "CREATE" => LogDetailType.Initial,
            "DELETE" => LogDetailType.BeforeDelete,
            _ => LogDetailType.Change
        };

        var info = new SystemLogUseCaseInfo
        {
            GroupName = DetermineGroupName(action, entityType),
            UseCaseTitle = GetHumanReadableActionTitle(action),
            RelatedTable = string.IsNullOrWhiteSpace(entityType) ? "System" : entityType,
            ActionCode = string.IsNullOrWhiteSpace(action) ? "EVENT" : action,
            ActionType = canonicalActionType,
            EntityType = string.IsNullOrWhiteSpace(entityType) ? "System" : entityType,
            EntityId = string.IsNullOrWhiteSpace(entityId) ? "—" : entityId,
            TargetDisplayName = targetDisplayName,
            Description = FormatReadableDescription(log),
            DetailType = detailType
        };

        info.FieldChanges = ExtractFieldChanges(log.OldValue, log.NewValue, action, entityType, actor, targetDisplayName);

        // Adjust DetailType if payload only has initial or deleted fields
        if (info.DetailType == LogDetailType.Change)
        {
            if (string.IsNullOrWhiteSpace(log.OldValue) && !string.IsNullOrWhiteSpace(log.NewValue) && canonicalActionType == "CREATE")
            {
                info.DetailType = LogDetailType.Initial;
            }
            else if (!string.IsNullOrWhiteSpace(log.OldValue) && string.IsNullOrWhiteSpace(log.NewValue) && canonicalActionType == "DELETE")
            {
                info.DetailType = LogDetailType.BeforeDelete;
            }
        }

        return info;
    }

    private static string DetermineGroupName(string action, string entityType)
    {
        if (action.Contains("STAFF") || action.Contains("USER") || action.Contains("PROFILE") || action.Contains("PASSWORD") ||
            entityType.Equals("User", StringComparison.OrdinalIgnoreCase) || entityType.Equals("Staff", StringComparison.OrdinalIgnoreCase))
        {
            return "ACCOUNT MANAGEMENT & ACCESS CONTROL";
        }

        if (action.Contains("COACH_APPLICATION") || action.Contains("CERTIFICATE") ||
            entityType.Contains("CoachProfile", StringComparison.OrdinalIgnoreCase) ||
            entityType.Contains("Certificate", StringComparison.OrdinalIgnoreCase))
        {
            return "COACH & CERTIFICATE VERIFICATION";
        }

        if (action.Contains("PAYOUT") || action.Contains("SUBSCRIPTION") || action.Contains("PLAN") ||
            action.Contains("PRICE") || action.Contains("PAYMENT") || action.Contains("GATEWAY") || action.Contains("REFUND"))
        {
            return "FINANCE, SUBSCRIPTIONS & PAYMENT GATEWAYS";
        }

        if (action.Contains("LOCATION") || action.Contains("REPORT") || action.Contains("APPEAL") || action.Contains("VIOLATION"))
        {
            return "OPERATIONS, REPORTS & LOCATIONS";
        }

        return "SYSTEM AUDIT TRAIL";
    }

    public static List<LogFieldChangeItem> ExtractFieldChanges(
        string? oldJson,
        string? newJson,
        string? action = null,
        string? entityType = null,
        string? actorName = null,
        string? targetDisplayName = null)
    {
        var oldDict = ParseRawDict(oldJson);
        var newDict = ParseRawDict(newJson);

        var actUpper = (action ?? string.Empty).Trim().ToUpperInvariant();
        var entUpper = (entityType ?? string.Empty).Trim().ToUpperInvariant();

        // Strip injected audit metadata keys ("actorRole", and "description" when injected by AuditLogService)
        oldDict.Remove("actorRole");
        newDict.Remove("actorRole");

        var isPlanDescriptionField = entUpper.Contains("SUBSCRIPTION") || entUpper.Contains("PLAN");
        if (!isPlanDescriptionField || !oldDict.ContainsKey("description"))
        {
            newDict.Remove("description");
            oldDict.Remove("description");
        }

        var isCreate = actUpper.Contains("CREATE") || actUpper.Contains("ADD") ||
                       (oldDict.Count == 0 && newDict.Count > 0 && !actUpper.Contains("DELETE") && !actUpper.Contains("REMOVE"));
        var isDelete = actUpper.Contains("DELETE") || actUpper.Contains("REMOVE") ||
                       (newDict.Count == 0 && oldDict.Count > 0);

        // Remove redundant actor/timestamp metadata keys that are already displayed in Actor & Activity cards
        foreach (var metaKey in RedundantAuditActorMetaKeys)
        {
            if (!oldDict.ContainsKey(metaKey))
            {
                newDict.Remove(metaKey);
            }
        }

        // Normalize/backfill pre-existing values for UPDATE / APPROVE / REJECT / LOCK / UNLOCK logs
        // where earlier backend versions only persisted partial fields in OldValue or NewValue
        if (!isCreate && !isDelete)
        {
            // Deduplicate refundAmount if approvedAmount is already present with the same value
            if (newDict.TryGetValue("approvedAmount", out var appAmt) &&
                newDict.TryGetValue("refundAmount", out var refAmt) &&
                string.Equals(appAmt?.Trim(), refAmt?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                newDict.Remove("refundAmount");
                oldDict.Remove("refundAmount");
            }

            foreach (var invKey in InvariantEntityContextKeys)
            {
                var hasOld = oldDict.TryGetValue(invKey, out var oldVal) && !string.IsNullOrWhiteSpace(oldVal);
                var hasNew = newDict.TryGetValue(invKey, out var newVal) && !string.IsNullOrWhiteSpace(newVal);

                if (hasOld && !hasNew)
                {
                    newDict[invKey] = oldVal!;
                }
                else if (hasNew && !hasOld)
                {
                    oldDict[invKey] = newVal!;
                }
            }

            foreach (var amtKey in new[] { "approvedAmount", "refundAmount", "amount" })
            {
                if (newDict.TryGetValue(amtKey, out var newAmt) && !string.IsNullOrWhiteSpace(newAmt) &&
                    (!oldDict.TryGetValue(amtKey, out var oldAmt) || string.IsNullOrWhiteSpace(oldAmt)))
                {
                    oldDict[amtKey] = "0";
                }
            }

            if (oldDict.TryGetValue("staffNote", out var rawOldStaffNote))
            {
                var cleanOld = ExtractCleanStaffNote(rawOldStaffNote);
                oldDict["staffNote"] = string.IsNullOrWhiteSpace(cleanOld) ? "None" : cleanOld;
            }

            if (newDict.TryGetValue("staffNote", out var rawNewStaffNote))
            {
                var cleanNew = ExtractCleanStaffNote(rawNewStaffNote);
                newDict["staffNote"] = string.IsNullOrWhiteSpace(cleanNew) ? "None" : cleanNew;
            }

            if (newDict.TryGetValue("staffNote", out var newNote) && !string.IsNullOrWhiteSpace(newNote) &&
                (!oldDict.TryGetValue("staffNote", out var oldNote) || string.IsNullOrWhiteSpace(oldNote)))
            {
                oldDict["staffNote"] = "None";
            }

            foreach (var txKey in new[] { "refundTransactionRef", "transactionRef", "gatewayRef" })
            {
                if (newDict.TryGetValue(txKey, out var newTx) && !string.IsNullOrWhiteSpace(newTx) &&
                    (!oldDict.TryGetValue(txKey, out var oldTx) || string.IsNullOrWhiteSpace(oldTx)))
                {
                    oldDict[txKey] = "Pending disbursement";
                }
            }

            foreach (var noteKey in new[] { "reason", "rejectionReason", "rejectedReason", "note", "appealReviewNote" })
            {
                if (newDict.TryGetValue(noteKey, out var newReason) && !string.IsNullOrWhiteSpace(newReason) &&
                    (!oldDict.TryGetValue(noteKey, out var oldReason) || string.IsNullOrWhiteSpace(oldReason)))
                {
                    oldDict[noteKey] = "None";
                }
            }

            if (newDict.TryGetValue("status", out var newStatus) && !string.IsNullOrWhiteSpace(newStatus) &&
                (!oldDict.TryGetValue("status", out var oldStatus) || string.IsNullOrWhiteSpace(oldStatus)))
            {
                oldDict["status"] = "PENDING";
            }
        }

        // Collect distinct keys in order
        var keys = new List<string>();
        foreach (var k in oldDict.Keys)
        {
            if (!keys.Contains(k, StringComparer.OrdinalIgnoreCase))
                keys.Add(k);
        }
        foreach (var k in newDict.Keys)
        {
            if (!keys.Contains(k, StringComparer.OrdinalIgnoreCase))
                keys.Add(k);
        }

        var allItems = new List<LogFieldChangeItem>();

        foreach (var key in keys)
        {
            // Skip target context-only keys in UPDATE/LOCK/UNLOCK/APPROVE/REJECT if they didn't change
            if (!isCreate && !isDelete && TargetContextOnlyKeys.Contains(key))
            {
                continue;
            }

            oldDict.TryGetValue(key, out var rawOld);
            newDict.TryGetValue(key, out var rawNew);

            string formattedOld;
            string formattedNew;

            if (isCreate)
            {
                formattedOld = "—";
                formattedNew = FormatValue(key, rawNew, actorName, targetDisplayName);
            }
            else if (isDelete)
            {
                formattedOld = FormatValue(key, rawOld, actorName, targetDisplayName);
                formattedNew = "—";
            }
            else
            {
                formattedOld = FormatValue(key, rawOld, actorName, targetDisplayName);
                formattedNew = FormatValue(key, rawNew, actorName, targetDisplayName);
            }

            var isStatus = key.IndexOf("status", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           key.Equals("isLocked", StringComparison.OrdinalIgnoreCase) ||
                           key.Equals("isActive", StringComparison.OrdinalIgnoreCase);

            var isChanged = isCreate || isDelete ||
                            !string.Equals(formattedOld, formattedNew, StringComparison.Ordinal);

            var item = new LogFieldChangeItem
            {
                Key = key,
                DisplayName = GetDisplayName(key),
                RawOldValue = isCreate ? null : rawOld,
                RawNewValue = isDelete ? null : rawNew,
                FormattedOldValue = formattedOld,
                FormattedNewValue = formattedNew,
                IsChanged = isChanged,
                IsStatus = isStatus
            };

            if (item.IsStatus)
            {
                item.OldStatusBadgeClass = GetStatusBadgeClass(formattedOld);
                item.NewStatusBadgeClass = GetStatusBadgeClass(formattedNew);
            }

            var textLen = Math.Max(formattedOld.Length, formattedNew.Length);
            item.IsLongText = textLen > 65 ||
                              formattedOld.Contains('\n') ||
                              formattedNew.Contains('\n') ||
                              IsJsonStructure(rawOld) ||
                              IsJsonStructure(rawNew);

            allItems.Add(item);
        }

        return allItems.Where(i => i.FormattedOldValue != "—" || i.FormattedNewValue != "—").ToList();
    }

    private static string GetDisplayName(string key)
    {
        if (FieldDisplayNames.TryGetValue(key, out var name))
            return name;

        var spaced = Regex.Replace(key, "([a-z])([A-Z])", "$1 $2").Replace('_', ' ');
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(spaced);
    }

    private static readonly (string Pattern, string Replacement)[] PhraseTranslations = new[]
    {
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
        ("E2: transfer rejected", "E2: Transfer rejected by banking gateway"),
        ("transfer rejected", "Transfer rejected"),
        ("E1: account not found", "E1: Account not found"),
        ("E3: invalid amount", "E3: Invalid amount"),
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
            // Fallback to string cleaning
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

    private static string FormatValue(string key, string? rawValue, string? actorName = null, string? targetDisplayName = null)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return "—";

        var trimmed = rawValue.Trim();
        if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase)) return "—";

        var keyLower = key.ToLowerInvariant();

        // Translate raw GUIDs in actor/reviewer or target reference fields into human-readable names
        if (Guid.TryParse(trimmed, out _))
        {
            if ((keyLower.Contains("verifiedby") || keyLower.Contains("approvedby") ||
                 keyLower.Contains("resolvedby") || keyLower.Contains("lockedby") ||
                 keyLower.Contains("processedby") || keyLower.Contains("reviewedby")) &&
                !string.IsNullOrWhiteSpace(actorName))
            {
                return actorName;
            }

            if (!string.IsNullOrWhiteSpace(targetDisplayName) && targetDisplayName != "—")
            {
                return targetDisplayName;
            }
        }

        // 0. If value is JSON object or array, parse and format as human-readable text
        if (IsJsonStructure(trimmed))
        {
            return FormatJsonValue(trimmed);
        }

        // 1. Boolean formats
        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            if (keyLower.Contains("lock")) return "Locked";
            if (keyLower.Contains("active")) return "Active";
            return "True";
        }
        if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
        {
            if (keyLower.Contains("lock")) return "Unlocked";
            if (keyLower.Contains("active")) return "Inactive";
            return "False";
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
            && dt.Year > 1900)
        {
            if (keyLower.Contains("birth"))
            {
                return dt.ToString("yyyy-MM-dd");
            }
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
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
        if (string.IsNullOrWhiteSpace(status) || status == "—") return "status-default";
        var s = status.Trim().ToUpperInvariant();

        if (s.Contains("APPROV") || s.Equals("ACTIVE") || s.Equals("UNLOCKED") || s.Contains("VERIF") || s.Contains("RESOLV") || s.Contains("COMPLET") || s.Contains("SUCCESS"))
            return "status-success";

        if (s.Contains("PENDING") || s.Contains("WAIT") || s.Contains("REVIEW") || s.Contains("SUBMIT") || s.Contains("PARTIAL"))
            return "status-warning";

        if (s.Contains("REJECT") || s.Equals("LOCKED") || s.Contains("INACTIVE") || s.Contains("DEACTIV") || s.Contains("FAIL") || s.Contains("CANCEL") || s.Contains("DISMISS") || s.Contains("BLOCK"))
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
                    if (prop.Value.ValueKind == JsonValueKind.Null)
                    {
                        dict[prop.Name] = string.Empty;
                    }
                    else
                    {
                        dict[prop.Name] = prop.Value.ToString();
                    }
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
        sb.AppendLine($"   - Action Type: {info.ActionType}");
        sb.AppendLine($"   - Action Code: {info.ActionCode}");
        sb.AppendLine($"   - Entity Type: {info.EntityType}");
        sb.AppendLine($"   - Entity ID: {info.EntityId}");
        sb.AppendLine($"   - Target Name: {info.TargetDisplayName}");
        sb.AppendLine("   - Description:");
        sb.AppendLine($"     \"{info.Description}\"");
        sb.AppendLine("   - Change History (Field | Before | After):");

        if (info.FieldChanges.Count == 0)
        {
            sb.AppendLine("     (No field changes recorded)");
        }
        else
        {
            foreach (var f in info.FieldChanges)
            {
                sb.AppendLine($"     * {f.DisplayName} ({f.Key}): [{f.FormattedOldValue}] -> [{f.FormattedNewValue}]");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
