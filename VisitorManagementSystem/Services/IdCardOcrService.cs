using System.Text.RegularExpressions;
using Tesseract;

namespace VisitorManagementSystem.Services
{
    public class IdCardOcrService
    {
        private readonly IWebHostEnvironment _environment;

        public IdCardOcrService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public string ExtractText(string imagePath)
        {
            var tessDataPath = Path.Combine(
                _environment.ContentRootPath,
                "TesseractData"
            );

            using var engine = new TesseractEngine(
                tessDataPath,
                "eng",
                EngineMode.Default
            );

            using var image = Pix.LoadFromFile(imagePath);

            using var page = engine.Process(
                image,
                PageSegMode.Auto
            );

            return page.GetText();
        }

        public string? ExtractNationalId(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Find exactly 14 consecutive digits
            var match = Regex.Match(text, @"\b\d{14}\b");

            if (match.Success)
            {
                return match.Value;
            }

            return null;
        }
    }
}