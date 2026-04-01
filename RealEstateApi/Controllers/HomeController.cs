using Microsoft.AspNetCore.Mvc;

namespace RealEstateApi.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
