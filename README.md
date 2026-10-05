# 🐾 VetCare Connect

ERP + CRM system for veterinary clinics, built with **ASP.NET Core MVC (.NET 10)** and
**EF Core / SQL Server**. Covers appointments, pet records, treatment records, billing,
inventory, suppliers, vaccination reminders, loyalty/feedback, reports, and audit logging.

## Prerequisites

1. **.NET 10 SDK** — `dotnet --version` should print `10.x`
2. **SQL Server LocalDB** — included with Visual Studio; standalone:
   `winget install Microsoft.SqlServer.SqlLocalDB` (then `sqllocaldb create MSSQLLocalDB`)

> The database (`VetCareDB`) is created and seeded with demo data automatically the
> first time the app starts. No migration commands needed.

## Run it

From the project folder (works in any terminal / editor):

```bash
dotnet run
```

Then open **http://localhost:5194**

| How | Steps |
|-----|-------|
| **CLI** | `dotnet run` (or `dotnet watch` for hot reload) |
| **VS Code** | Open the folder → install the recommended C# Dev Kit extension → press **F5** |
| **Antigravity** | Open the folder → same `.vscode` config as VS Code → **F5**; agents follow `AGENTS.md` |
| **OpenCode** | Open the folder in a terminal → run `opencode` → it reads `AGENTS.md`; or just `dotnet run` |

## Demo accounts

Seeded on first run only, and **only when the database is empty**.

| Role | Username | Password |
|------|----------|----------|
| Administrator | `admin`  | see below |
| Veterinarian | `vet` | see below |
| Clinic Staff | `staff` | see below |
| Pet Owner | `owner` | see below |
| Supplier | `supplier` | see below |

Passwords are **never stored in source control**. Supply them with user-secrets or
environment variables, otherwise a strong random one is generated per account and
printed to the console on first run:

```bash
dotnet user-secrets set "Seed:Administrator:Password" "<your-password>"
dotnet user-secrets set "Seed:Veterinarian:Password" "<your-password>"
dotnet user-secrets set "Seed:ClinicStaff:Password"  "<your-password>"
dotnet user-secrets set "Seed:PetOwner:Password"     "<your-password>"
dotnet user-secrets set "Seed:Supplier:Password"     "<your-password>"
```

On a hosting provider set the matching `Seed__Administrator__Password` style
environment variables instead. The seeded login names and e-mail addresses stay fixed.

## Project structure

```
Controllers/   MVC controllers (one per module)
Models/        EF Core entities
Data/          DbContext + startup DB initializer/seeder
Views/         Razor views (public site + role dashboards)
Services/      Audit logging, service fee table
Helpers/       Claims helpers (current user id / role)
wwwroot/       CSS, JS, images
```
