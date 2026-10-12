using Microsoft.AspNetCore.Mvc;
using VisitorManagementSystem.Services;

namespace VisitorManagementSystem.Controllers
{
    public class OcrController : Controller
    {
        private readonly IdCardOcrService _ocrService;

        public OcrController(IdCardOcrService ocrService)
        {
            _ocrService = ocrService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Scan(IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                ViewBag.Error = "Please select an image.";
                return View("Index");
            }

            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads"
            );

            Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                image.CopyTo(stream);
            }
            var text = _ocrService.ExtractText(filePath);

            var nationalId = _ocrService.ExtractNationalId(text);

            ViewBag.OcrText = text;
            ViewBag.NationalId = nationalId;
            return View("Index");
        }
    }
}