namespace EcommerceApp.Models.ViewModels;

public class CustomerSegmentIndexViewModel
{
    public IReadOnlyList<CustomerSegmentSummaryViewModel> Segments { get; set; } = Array.Empty<CustomerSegmentSummaryViewModel>();
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
}

public class CustomerSegmentSummaryViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleDescription { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastOrderAt { get; set; }
}

public class CustomerSegmentDetailViewModel
{
    public CustomerSegmentSummaryViewModel Segment { get; set; } = new();
    public IReadOnlyList<CustomerSegmentMemberViewModel> Members { get; set; } = Array.Empty<CustomerSegmentMemberViewModel>();
}

public class CustomerSegmentMemberViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
