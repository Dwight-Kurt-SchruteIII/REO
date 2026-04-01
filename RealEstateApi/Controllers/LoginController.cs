using Microsoft.AspNetCore.Mvc;

namespace RealEstateApi.Controllers
{
    public class LoginController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
