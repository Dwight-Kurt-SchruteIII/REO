using Microsoft.AspNetCore.Mvc;

namespace RealEstateApi.Controllers
{
    public class TenantsController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
