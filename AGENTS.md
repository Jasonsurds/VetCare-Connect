# VetCare Connect — Agent Guide

VetCare Connect is an ASP.NET Core MVC web application (.NET 10) for a veterinary clinic:
ERP + CRM covering appointments, pets, treatment records, billing, inventory, suppliers,
vaccination reminders, CRM/loyalty, reports, and audit logging.

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` should print 10.x)
- **SQL Server LocalDB** (`sqllocaldb info` should work) — connection string is in `appsettings.json` (`VetCareDb`)
- The database is created and seeded automatically on first app start (`Data/DbInitializer.cs`) — no manual migration step

## Run

```bash
dotnet run
```

- Listens on **http://localhost:5194** (from `Properties/launchSettings.json`)
- `dotnet watch` also works for hot reload
- F5 works in VS Code / Antigravity via `.vscode/launch.json`

## Seeded demo accounts

| Role          | Username   | Password     |
|---------------|------------|--------------|
| Administrator | `admin`    | `admin123`   |
| Veterinarian  | `vet`      | `vet123`     |
| Veterinarian  | `vet2`     | `vet123`     |
| Clinic Staff  | `staff`    | `staff123`   |
| Pet Owner     | `owner`    | `owner123`   |
| Pet Owner     | `owner2`   | `owner123`   |
| Supplier      | `supplier` | `supplier123`|

## Project layout

- `Controllers/` — one controller per module; authorization is enforced in-controller
  (`[Authorize]` attributes plus role/ownership checks inside actions)
- `Models/` — EF Core entities; relationships and delete behavior are configured in `Data/VetCareDbContext.cs`
- `Views/` — Razor views; public site uses `Views/Shared/_Layout.cshtml`,
  authenticated pages use `_DashboardLayout.cshtml`
- `Data/DbInitializer.cs` — creates the database and seeds demo data at startup
- `Services/AuditService.cs` — `IAuditService.LogAsync(action, entity, details)`; call it
  after every create/update/delete and login/logout
- `Helpers/` — claims extensions (`User.GetUserId()`, `User.GetUserRole()`)
- `wwwroot/js/dashboard.js` — wires `form[data-confirm="..."]` to a JS confirm dialog
  (use this attribute for any destructive action)

## Conventions

- After any write action, set `TempData["SuccessMessage"]` and redirect (Post-Redirect-Get).
- Role names are plain strings: `Administrator`, `Clinic Staff`, `Veterinarian`, `Pet Owner`, `Supplier`.
- Pet owners may only ever see/modify their own pets and appointments — keep the
  ownership checks (`appointment.Pet.OwnerID == User.GetUserId()`) intact.
- UI building blocks (cards, tables, buttons, badges) are CSS classes in
  `wwwroot/css/dashboard.css` (`vc-card`, `vc-table`, `btn-vc`, `btn-vc-outline`,
  `btn-vc-danger`, `_StatusBadge` partial for status pills).

## Database

SQL Server via EF Core. Deleting an `Appointment` cascades to its `TreatmentRecord`
and `Billing` (configured in `OnModelCreating`) — warn users about this before
permanent deletes of completed visits.
