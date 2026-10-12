using VisitorManagementSystem.Models;

namespace VisitorManagementSystem.ViewModels
{
    public class DashboardViewModel
    {
        public List<Visit> Visits { get; set; } = new();

        public string Period { get; set; } = "Today";

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public string? Status { get; set; }

        public int? VisitTypeId { get; set; }

        public string? Search { get; set; }

        public int CurrentPage { get; set; }

        public int TotalPages { get; set; }

        public List<VisitType> VisitTypes { get; set; } = new();

        public List<Receptionist> Receptionists { get; set; } = new();

        public List<string> PhonesWithHistory { get; set; } = new();

        public int TotalVisitsToday { get; set; }

        public int CurrentlyInside { get; set; }

        public int CheckedOutToday { get; set; }



    }
}