using System.Diagnostics;
using Azure;
using Azure.AI.ContentSafety;
using ContentSafetyAI.Models;
using Microsoft.AspNetCore.Mvc;

namespace ContentSafetyAI.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public JsonResult ValidateImageContent(string images)
        {
            int violationCount = 0;
            if (!string.IsNullOrEmpty(images))
            {
                // API settings
                string endpoint = _configuration["ContentSafetyEndPoint"]!;
                string apiKey = _configuration["ContentSafetyAPIKey"]!;

                // Split multiple images
                string[] ImageArray = images.Split(',');

                foreach (string image in ImageArray)
                {
                    byte[] imageData = Convert.FromBase64String(image);

                    ContentSafetyClient contentSafetyClient = new ContentSafetyClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                    //BlocklistClient blocklistClient = new BlocklistClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                    ContentSafetyImageData uploadedImage = new ContentSafetyImageData(BinaryData.FromBytes(imageData));

                    // Analyze image 
                    var request = new AnalyzeImageOptions(uploadedImage);
                    Response<AnalyzeImageResult> response;
                    try
                    {
                        response = contentSafetyClient.AnalyzeImage(request);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = ex.Message + "----------------" + ex.Source });
                    }

                    // Response values
                    int Hate = response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == ImageCategory.Hate)?.Severity ?? 0;
                    int Selfharm = response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == ImageCategory.SelfHarm)?.Severity ?? 0;
                    int Sexual = response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == ImageCategory.Sexual)?.Severity ?? 0;
                    int Violence = response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == ImageCategory.Violence)?.Severity ?? 0;

                    // Check the response
                    if (Hate != 0 || Selfharm != 0 || Sexual != 0 || Violence != 0)
                    {
                        var violations = new List<string>();
                        if (Hate != 0) violations.Add($"Hate (Severity: {Hate})");
                        if (Selfharm != 0) violations.Add($"Self-harm (Severity: {Selfharm})");
                        if (Sexual != 0) violations.Add($"Sexual (Severity: {Sexual})");
                        if (Violence != 0) violations.Add($"Violence (Severity: {Violence})");

                        string message = "Image contains: " + string.Join(", ", violations);
                        return Json(new { success = false, message });

                    }

                }
            }
            
            return Json(new { success = true });

        }
    }
}
