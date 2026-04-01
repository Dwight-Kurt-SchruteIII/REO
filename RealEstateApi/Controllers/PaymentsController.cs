using Microsoft.AspNetCore.Mvc;

namespace RealEstateApi.Controllers
{
    public class PaymentsController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
