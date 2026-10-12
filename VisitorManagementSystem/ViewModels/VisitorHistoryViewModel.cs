namespace VisitorManagementSystem.ViewModels
{
    public class VisitorHistoryViewModel
    {
        public int VisitorId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? CompanyName { get; set; }

        public DateTime LastVisit { get; set; }

        public int VisitCount { get; set; }
    }
}