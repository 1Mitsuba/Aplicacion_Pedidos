using System.Diagnostics;
using Aplicacion_Pedidos.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aplicacion_Pedidos.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "Bienvenido a Sistema de Pedidos";
            ViewData["Message"] = "Sistema de gestión de pedidos y productos";
            return View();
        }

        public IActionResult About()
        {
            ViewData["Title"] = "Acerca de";
            ViewData["Message"] = "Sistema de gestión para control de pedidos";
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Title"] = "Contacto";
            ViewData["Message"] = "Información de contacto";
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
    }
}
