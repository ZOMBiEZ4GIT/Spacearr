using Spacearr.Data.Entities;
using Spacearr.Library;

namespace Spacearr.Actions;

public sealed record ActionRequest(ActionType Type, int ItemId, int? TargetProfileId, bool Unmonitor = false, string? ConfirmToken = null);
public sealed record ActionStep(string Description, string Method, string Path);
public sealed record ActionPreview(ActionRequest Request, string Title, string InstanceName, ArrType InstanceType, long BytesFreedNow, SavingsEstimate? Estimate, string? Warning, ActionStep[] Steps, string ConfirmToken, DateTime ExpiresAt);

public sealed class ActionPlanException : Exception
{
    public ActionPlanException(string message) : base(message) { }
}
