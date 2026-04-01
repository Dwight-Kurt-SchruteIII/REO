using Microsoft.AspNetCore.Mvc;

namespace RealEstateApi.Controllers
{
    public class PropertiesController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
