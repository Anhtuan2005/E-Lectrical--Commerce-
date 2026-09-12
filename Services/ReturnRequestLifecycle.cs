using EcommerceApp.Models;

namespace EcommerceApp.Services;

public static class ReturnRequestLifecycle
{
    public static bool CanTransition(string current, string next)
    {
        if (!ReturnWarrantyRequestStatuses.All.Contains(next)) return false;
        if (current == next) return true;
        return (current, next) switch
        {
            (ReturnWarrantyRequestStatuses.Submitted, ReturnWarrantyRequestStatuses.Reviewing or ReturnWarrantyRequestStatuses.Rejected) => true,
            (ReturnWarrantyRequestStatuses.Reviewing, ReturnWarrantyRequestStatuses.WaitingForCustomer or ReturnWarrantyRequestStatuses.Approved or ReturnWarrantyRequestStatuses.Rejected) => true,
            (ReturnWarrantyRequestStatuses.WaitingForCustomer, ReturnWarrantyRequestStatuses.Reviewing or ReturnWarrantyRequestStatuses.Rejected) => true,
            (ReturnWarrantyRequestStatuses.Approved, ReturnWarrantyRequestStatuses.Completed or ReturnWarrantyRequestStatuses.Rejected) => true,
            _ => false
        };
    }
}
