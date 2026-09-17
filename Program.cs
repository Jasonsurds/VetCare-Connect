using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Models;
using VetCare.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<VetCareDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("VetCareDb")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        options.Events.OnTicketReceived = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<VetCareDbContext>();
            var audit = context.HttpContext.RequestServices.GetRequiredService<IAuditService>();
            var hasher = new PasswordHasher<User>();

            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                context.Fail("Your Google account does not provide an email address.");
                return;
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                var baseName = email.Split('@')[0];
                var userName = baseName;
                var suffix = 1;
                while (await db.Users.AnyAsync(u => u.UserName == userName))
                    userName = $"{baseName}{suffix++}";

                user = new User
                {
                    Role = "Pet Owner",
                    Name = context.Principal?.FindFirstValue(ClaimTypes.Name) ?? "Pet Owner",
                    UserName = userName,
                    Email = email,
                    Password = hasher.HashPassword(new User(), Guid.NewGuid().ToString("N")),
                    CreatedDate = DateTime.Now
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
                await audit.LogAsync("Register", "Users", $"{user.Name} auto-registered via Google sign-in.", user.Name);
            }

            if (!user.IsActive)
            {
                context.Fail("Your VetCare Connect account has been deactivated.");
                return;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new(ClaimTypes.Name, user.Name),
                new(ClaimTypes.Role, user.Role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            context.Principal = new ClaimsPrincipal(identity);

            await audit.LogAsync("Login", "Users", $"{user.Name} ({user.Role}) signed in via Google.", user.Name);
        };
    });
}

builder.Services.AddAuthorization();

var app = builder.Build();

// Create the database (if missing) and seed demo data on first run.
using (var scope = app.Services.CreateScope())
{
    DbInitializer.Initialize(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "login",
    pattern: "login",
    defaults: new { controller = "Account", action = "Login" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
