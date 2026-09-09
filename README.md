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

| Role | Username | Password |
|------|----------|----------|
| Administrator | `admin` | `admin123` |
| Veterinarian | `vet` / `vet2` | `vet123` |
| Clinic Staff | `staff` | `staff123` |
| Pet Owner | `owner` / `owner2` | `owner123` |
| Supplier | `supplier` | `supplier123` |

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
