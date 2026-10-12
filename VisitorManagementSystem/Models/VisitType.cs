namespace VisitorManagementSystem.Models
{
    public class VisitType
    {
        public int Id { get; set; }

        public string NameEn { get; set; }

        public string NameAr { get; set; }

        public ICollection<Visit> Visits { get; set; }
    }
}
