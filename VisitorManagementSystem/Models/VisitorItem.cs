namespace VisitorManagementSystem.Models
{
    public class VisitorItem
    {
        public int Id { get; set; }

        public int VisitId { get; set; }

        public Visit Visit { get; set; }

        public string ItemName { get; set; }

        public int Quantity { get; set; }
    }
}
