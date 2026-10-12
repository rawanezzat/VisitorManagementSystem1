using System.ComponentModel.DataAnnotations;
using VisitorManagementSystem.Models;

namespace VisitorManagementSystem.ViewModels
{
    public class AddVisitViewModel
    {
        // Existing Visitor
        public int? VisitorId { get; set; }

        // Visitor Information
        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? CompanyName { get; set; }

        // Visit Information
        [Required]
        public int VisitTypeId { get; set; }

        public string? OtherVisitType { get; set; }

        // Receptionist
        [Required]
        public int ReceptionistId { get; set; }

        // ID
        public bool HasId { get; set; }

        public string? NationalId { get; set; }

        public string? EmployeeToMeet { get; set; }

        public string? Department { get; set; }
        // Items
        public List<VisitItemViewModel> Items { get; set; } = new();

        // Dropdowns
        public List<VisitType> VisitTypes { get; set; } = new();

        public List<Receptionist> Receptionists { get; set; } = new();
    }

    public class VisitItemViewModel
    {
        public string? ItemName { get; set; }

        public int Quantity { get; set; }
    }
}