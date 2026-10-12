namespace VisitorManagementSystem.Models
{
    public class Visitor
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string? Phone { get; set; }

        public string? Email { get; set; }

        public string? NationalId { get; set; }


        public ICollection<Visit> Visits { get; set; } = new List<Visit>();
    }
}
