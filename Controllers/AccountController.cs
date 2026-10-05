using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
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
        private readonly INotificationService _notif;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<User> _hasher = new();

        public AccountController(VetCareDbContext db, IAuditService audit, INotificationService notif, IConfiguration config)
        {
            _db = db;
            _audit = audit;
            _notif = notif;
            _config = config;
        }

        // Google sign-in is only wired up when both credentials are supplied (see Program.cs),
        // so the UI hides the button instead of offering a link that would throw.
        private bool GoogleEnabled =>
            !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientId"]) &&
            !string.IsNullOrWhiteSpace(_config["Authentication:Google:ClientSecret"]);

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["GoogleEnabled"] = GoogleEnabled;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? role, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["GoogleEnabled"] = GoogleEnabled;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Please enter both username/email and password.");
                return View();
            }

            var lookup = email.Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.UserName == lookup || (u.Email != null && u.Email == lookup));

            if (user == null)
            {
                await _audit.LogAsync("Login Failed", "Users", $"Invalid login attempt for '{lookup}'.", lookup);
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View();
            }

            if (!user.IsActive)
            {
                await _audit.LogAsync("Login Failed", "Users", $"Sign-in blocked — account '{user.UserName}' is deactivated.", user.Name);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact the clinic to restore access.");
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
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string name, string userName, string password, string confirmPassword, string? email, string? contactNumber, string? address, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            var fullName = (name ?? string.Empty).Trim();
            var username = (userName ?? string.Empty).Trim();
            var mail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Name, username and password are required.");
                return View();
            }

            if (password.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Password must be at least 6 characters long.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "The passwords you entered do not match.");
                return View();
            }

            if (await _db.Users.AnyAsync(u => u.UserName == username))
            {
                ModelState.AddModelError(string.Empty, $"Username '{username}' is already taken.");
                return View();
            }

            // Sign-in accepts either username or email, so a duplicate email would make
            // the account ambiguous to log into.
            if (mail != null && await _db.Users.AnyAsync(u => u.Email == mail))
            {
                ModelState.AddModelError(string.Empty, "An account already exists with that email address.");
                return View();
            }

            var owner = new User
            {
                // Hardcoded on purpose — self-registration must never grant staff or admin roles.
                Role = "Pet Owner",
                Name = fullName,
                UserName = username,
                Email = mail,
                ContactNumber = string.IsNullOrWhiteSpace(contactNumber) ? null : contactNumber.Trim(),
                Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                IsActive = true
            };
            owner.Password = _hasher.HashPassword(owner, password);

            _db.Users.Add(owner);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Create", "Users", $"Self-registered pet owner '{fullName}' (ID {owner.UserID}).");

            // Let the clinic know a new public account now exists so they can review/deactivate it if needed.
            var contactHint = string.IsNullOrWhiteSpace(mail) ? owner.UserName : mail;
            await _notif.SendToRoleAsync("Administrator", "New Pet Owner Registered",
                $"'{fullName}' (username: {owner.UserName}, contact: {contactHint}) created a public pet owner account and can now sign in.",
                "System", $"/Users/Edit/{owner.UserID}");

            TempData["SuccessMessage"] = $"Account created for {fullName}. Please sign in to continue.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        [HttpGet]
        public IActionResult GoogleLogin()
        {
            if (!GoogleEnabled)
            {
                TempData["ErrorMessage"] = "Google sign-in is not configured on this server.";
                return RedirectToAction(nameof(Login));
            }

            var properties = new AuthenticationProperties { RedirectUri = "/" };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ManageProfile()
        {
            var user = await _db.Users.FindAsync(User.GetUserId());
            if (user == null) return NotFound();
            ViewData["Title"] = "Manage Profile";
            ViewData["DashTitle"] = "Manage Profile";
            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageProfile(string name, string? email, string? contactNumber, string? address, string? newPassword)
        {
            var user = await _db.Users.FindAsync(User.GetUserId());
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(nameof(name), "Name is required.");
            }

            if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length < 6)
            {
                ModelState.AddModelError(nameof(newPassword), "Password must be at least 6 characters.");
            }

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Manage Profile";
                ViewData["DashTitle"] = "Manage Profile";
                return View(user);
            }

            user.Name = name.Trim();
            user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            user.ContactNumber = string.IsNullOrWhiteSpace(contactNumber) ? null : contactNumber.Trim();
            user.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            if (!string.IsNullOrWhiteSpace(newPassword))
                user.Password = _hasher.HashPassword(user, newPassword);

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Update", "Users",
                $"{user.Name} updated their own profile.", user.Name);

            TempData["SuccessMessage"] = "Your profile has been updated.";
            return RedirectToAction(nameof(ManageProfile));
        }
    }
}
