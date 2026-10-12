using Microsoft.AspNetCore.Mvc;

namespace VisitorManagementSystem.Resources
{
    public class SharedResource : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
