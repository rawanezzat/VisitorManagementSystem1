namespace VisitorManagementSystem.Models
{
    public class Visit
    {
        public int Id { get; set; }

        public int VisitorId { get; set; }
        public Visitor Visitor { get; set; }

        public string? CompanyName { get; set; }

        public string? EmployeeToMeet { get; set; }

        public string? Department { get; set; }

        public int VisitTypeId { get; set; }
        public VisitType VisitType { get; set; }

        public string? OtherVisitType { get; set; }

        public int ReceptionistId { get; set; }
        public Receptionist Receptionist { get; set; }

        public bool HasId { get; set; }


        public DateTime CheckInTime { get; set; }

        public DateTime? CheckOutTime { get; set; }

        public string Status { get; set; }

        public bool IsDeleted { get; set; }

        public ICollection<VisitorItem> Items { get; set; }
    }
}
