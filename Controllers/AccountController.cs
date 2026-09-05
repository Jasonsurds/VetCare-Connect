using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;
using VetCare.Models;
using VetCare.Services;

namespace VetCare.Controllers
{
    public class AccountController : Controller
    {
        private readonly VetCareDbContext _db;
        private readonly IAuditService _audit;
        private readonly PasswordHasher<User> _hasher = new();

        public AccountController(VetCareDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? role, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Please enter both username/email and password.");
                return View();
            }

            var lookup = email.Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.UserName == lookup || (u.Email != null && u.Email == lookup));

            if (user == null || !user.IsActive)
            {
                await _audit.LogAsync("Login Failed", "Users", $"Invalid login attempt for '{lookup}'.", lookup);
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View();
            }

            var result = _hasher.VerifyHashedPassword(user, user.Password, password);
            if (result == PasswordVerificationResult.Failed)
            {
                await _audit.LogAsync("Login Failed", "Users", $"Wrong password for '{lookup}'.", user.Name);
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View();
            }

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
                user.Password = _hasher.HashPassword(user, password);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new(ClaimTypes.Name, user.Name),
                new(ClaimTypes.Role, user.Role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = rememberMe });

            await _audit.LogAsync("Login", "Users", $"{user.Name} ({user.Role}) signed in.", user.Name);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
                await _audit.LogAsync("Logout", "Users", $"{User.Identity?.Name} signed out.");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}
