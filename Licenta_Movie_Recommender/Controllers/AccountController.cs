using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Licenta_Movie_Recommender.Data;
using Licenta_Movie_Recommender.Models;

using System.Security.Claims;

namespace Licenta_Movie_Recommender.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // afiseaza pagina de inregistrare
        [HttpGet]
        public IActionResult Register() => View();

        // cand apasa butonul de creare cont
        [HttpPost]
        public async Task<IActionResult> Register(string username, string email)
        {
            if (_context.Users.Any(u => u.Username == username))
            {
                ViewBag.Error = "Numele este deja folosit!";
                return View();
            }

            var user = new User { Username = username, Email = email };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await SignInUser(user);
            return RedirectToAction("Index", "Movies");
        }

        // afiseaza pagina de login
        [HttpGet]
        public IActionResult Login() => View();

        // cand apasa butonul de logare
        [HttpPost]
        public async Task<IActionResult> Login(string username)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                ViewBag.Error = "Utilizatorul nu exista!";
                return View();
            }

            await SignInUser(user);
            return RedirectToAction("Index", "Movies");
        }

        // cand apasa pe delogare
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // functia care seteaza  cookie ul in browser
        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }
    }
}