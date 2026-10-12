namespace VisitorManagementSystem.Models
{
    public class Receptionist
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public ICollection<Visit> Visits { get; set; }
    }
}
