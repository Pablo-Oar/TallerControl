namespace TallerControl.Domain.Work;

public enum WorkOrderStatus
{
    Pending,
    InProgress,
    WaitingParts,
    Completed,
    Delivered,
    Cancelled,
}
