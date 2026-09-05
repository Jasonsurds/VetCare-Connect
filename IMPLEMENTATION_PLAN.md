# VetCare Connect — Remaining Implementation Plan

**Project:** VetCare Connect: ERP and CRM System for Veterinary Clinics
**Reference document:** `final docs 1.pdf` (Final Project Documentation — IT15 Integrative Programming and Technologies)
**Prepared:** 2026-09-05
**Scope:** This plan covers **only the work that is not yet done**, based on a code review of the current repository against the requirements in the attached documentation.

---

## 1. Current State (What Is Actually Done)

Verified by code review of `Controllers/`, `Models/`, `Views/`, `Program.cs`:

| # | Requirement item | Status | Notes |
|---|------------------|--------|-------|
| 1 | ASP.NET Core MVC project skeleton (.NET 10) | ✅ Done | `VetCare.csproj`, `Program.cs` with default route + `/login` route |
| 2 | Public landing page | ✅ Done (static) | `Views/Home/Index.cshtml` — marketing page, hero, features, contact sections |
| 3 | Login page UI | ✅ Done (UI only) | `Views/Account/Login.cshtml` — role chips for 5 roles, remember-me, Google button, forgot password |
| 4 | Real authentication | ❌ **Simulated only** | `AccountController.Login` POST accepts **any** email/password and just sets `TempData` + redirects. No credential check, no cookie/session, no redirect to a dashboard |
| 5 | Database | ❌ Not started | No EF Core / SQL Server connection, no entities, no migrations. `appsettings.json` has no connection string |
| 6 | All 10 functional modules | ❌ Not started | See §3 |
| 7 | RBAC (5 roles) | ❌ Not started | Role chips on the login form are cosmetic; nothing enforces roles |
| 8 | Security features (2FA, audit logs, encryption, session mgmt) | ❌ Not started | See §5 |
| 9 | External APIs (Firebase, PayMongo, Notification) | ❌ Not started | Google sign-in button and "forgot password" are toast placeholders |

**Bottom line:** the system today is a presentation shell. Everything below is remaining work.

---

## 2. Gap Summary — Requirements vs. Current System

| Module (per doc) | Data table (per data dictionary) | Status |
|------------------|----------------------------------|--------|
| 1. Pet & Owner Management | `Users`, `Pets` | ❌ Not started |
| 2. Appointment Scheduling | `Appointments` | ❌ Not started (landing page modal is cosmetic only) |
| 3. Veterinarian Management | `Users` (Role = Veterinarian) | ❌ Not started |
| 4. Treatment Records | `TreatmentRecords` | ❌ Not started |
| 5. Medicine Inventory | `Inventory` | ❌ Not started |
| 6. Billing | `Billing` | ❌ Not started |
| 7. Vaccination Reminders | `VaccinationReminders` | ❌ Not started |
| 8. Customer CRM | `CRM` | ❌ Not started |
| 9. Supplier Management | `Suppliers` | ❌ Not started |
| 10. Reports | `Reports` | ❌ Not started |
| Role-based access control | `Users.Role` | ❌ Not started |
| Two-factor authentication | — | ❌ Not started |
| Audit logs | — (new table needed) | ❌ Not started |
| Session management | — | ❌ Not started |

---

## 3. Foundation Work (Build This First — Phase 1)

Nothing else can be built until data + auth exist. Order matters.

### 3.1 Database layer (SQL Server / SSMS + EF Core)

The doc specifies **SSMS (SQL Server)** for secure data storage. Use EF Core with the SQL Server provider.

**Steps:**

1. Add NuGet packages:
   ```
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer
   dotnet add package Microsoft.EntityFrameworkCore.Design
   dotnet tool install --global dotnet-ef
   ```
2. Create the `VetCareDB` database in SSMS, then add the connection string to `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "VetCareDb": "Server=localhost\\SQLEXPRESS;Database=VetCareDB;Trusted_Connection=True;TrustServerCertificate=True;"
   }
   ```
   (Use `Server=(localdb)\MSSQLLocalDB;...` if LocalDB is preferred over a full SQL Server instance; use SQL auth credentials + `Encrypt=True` when targeting a hosted server.)
3. Create `Data/VetCareDbContext.cs` with entities matching the **data dictionary exactly** (the doc's ERD is the grading contract):

   | Entity class | Table | Key fields per data dictionary |
   |---|---|---|
   | `Models/User.cs` | Users | `UserId` (PK, AI), `Name`, `Role`, `UserName`, `Password` (store **hash**, not plain text) |
   | `Models/Pet.cs` | Pets | `PetId` (PK), `OwnerId` (FK→Users), `PetName`, `Species`, `Breed`, `Age`, `MedicalHistory` |
   | `Models/Appointment.cs` | Appointments | `AppointmentId` (PK), `PetId` (FK), `VetId` (FK), `AppointmentDate`, `Status` (Pending/Completed/Cancelled), `Notes` |
   | `Models/TreatmentRecord.cs` | TreatmentRecords | `TreatmentId` (PK), `AppointmentId` (FK), `Diagnosis`, `Prescription`, `TreatmentNotes` |
   | `Models/InventoryItem.cs` | Inventory | `ItemId` (PK), `ItemName`, `Category`, `Quantity`, `UnitPrice`, `ReorderLevel`, `SupplierId` (FK) |
   | `Models/Billing.cs` | Billing | `InvoiceId` (PK), `AppointmentId` (FK), `OwnerId` (FK), `TotalAmount`, `PaymentMethod` (Cash/Card/Online), `PaymentStatus` (Paid/Pending), `DateIssued` |
   | `Models/VaccinationReminder.cs` | VaccinationReminders | `ReminderId` (PK), `PetId` (FK), `VaccineName`, `DueDate`, `Status` (Sent/Pending) |
   | `Models/CrmRecord.cs` | CRM | `CrmId` (PK), `OwnerId` (FK), `Interaction`, `Feedback`, `LoyaltyPoints` |
   | `Models/Supplier.cs` | Suppliers | `SupplierId` (PK), `SupplierName`, `ContactInfo`, `ProductCatalog`, `ContractDetails` |
   | `Models/Report.cs` | Reports | `ReportId` (PK), `ReportType` (Clinical/Financial/Operational), `GeneratedBy` (FK→Users), `DateGenerated`, `Content` |
   | `Models/AuditLog.cs` *(added, required by doc's security features)* | AuditLogs | `AuditId` (PK), `UserId`, `Action`, `EntityName`, `EntityId`, `Timestamp`, `Details` |

4. Register the context in `Program.cs`, run `dotnet ef migrations add InitialCreate` and `dotnet ef database update`.

### 3.2 Real authentication (replace the simulated login)

Replace the body of `AccountController.Login` (POST) with a real credential check, using **cookie authentication** (simplest fit for the doc's single `Users` table):

1. `dotnet add package BCrypt.Net-Next` for password hashing.
2. In `Program.cs`: `builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)...` with login path `/Account/Login`, sliding expiration, `ExpireTimeSpan = 8h` (this also delivers the doc's **session management** item — set `SlidingExpiration = true`, secure cookie flags).
3. Rewrite `Login` POST:
   - Look up `User` by `UserName` (the seed uses plain usernames per the doc, but the form posts emails — accept both: match `UserName` or a seeded `email` convention like `admin@vetcare.com`).
   - Verify `BCrypt.Verify(password, user.Password)`.
   - Create `ClaimsIdentity` with claims: `NameIdentifier` = UserId, `Name` = Name, `Role` = Role.
   - `SignInAsync` with `IsPersistent = rememberMe`, then redirect **role-based**: Admin/Staff/Vet/Supplier → their dashboard, Owner → owner portal.
   - On failure: `ModelState.AddModelError` and redisplay (currently the form never fails — fix that).
4. Add `Logout` action (`[Authorize]`, `SignOutAsync()`) and wire the navbar sign-out button.
5. Remove the pre-filled password value `value="password123"` from the login form before final demo (it's a dev convenience).

### 3.3 Seed data (the doc's grading accounts)

Create `Data/DbSeeder.cs`, called on startup, inserting the exact accounts the documentation lists:

| Role | Username | Password |
|------|----------|----------|
| Administrator | `admin` | `admin123` |
| Veterinarian | `vet` | `vet123` |
| Clinic Staff | `staff` | `staff123` |
| Pet Owner | `owner` | `owner123` |
| Supplier | `supplier` | `supplier123` |

(Hash passwords with BCrypt at seed time.) Also seed demo rows so screens aren't empty for screenshots: 1–2 vets, 3 owners, 5 pets, appointments across statuses, 8–10 inventory items (some below reorder level), invoices (paid + pending), reminders (due soon), CRM entries, 2 suppliers.

### 3.4 RBAC plumbing

1. `builder.Services.AddAuthorization(...)` with policies per role:
   ```csharp
   options.AddPolicy("AdminOnly", p => p.RequireRole("Administrator"));
   options.AddPolicy("StaffOrAdmin", p => p.RequireRole("Administrator", "Clinic Staff"));
   options.AddPolicy("VetAccess", p => p.RequireRole("Administrator", "Veterinarian"));
   ```
2. Add an **`[Authorize]` default**: require authentication app-wide (`app.UseAuthorization()` already exists; add `FallbackPolicy = RequireAuthenticatedUser`) so no dashboard page leaks.
3. Map role → landing dashboard in `AccountController` after login.

### 3.5 App shell for authenticated users

`Views/Shared/_Layout.cshtml` is currently the public marketing navbar. Add `Views/Shared/_DashboardLayout.cshtml` (sidebar + topbar) used by all module views, with nav items filtered by role:

- **Administrator** → Dashboard, Pets & Owners, Appointments, Veterinarians, Treatments, Inventory, Billing, Reminders, CRM, Suppliers, Reports, Audit Logs, Users
- **Veterinarian** → My Schedule (Appointments), Treatment Records, Vaccination Reminders, Pets (read), Inventory (read)
- **Clinic Staff** → Appointments, Pets & Owners, Billing, Inventory, Reminders, CRM
- **Pet Owner** → My Pets, Book Appointment, My Appointments, My Invoices, My Reminders, Loyalty & Feedback
- **Supplier** → My Profile, Product Catalog (view items they supply)

Each module's Index page shows **summary cards** (today's appointments, pending invoices, low stock, reminders due) → this becomes the per-role Dashboard the doc's prototype screenshots need.

---

## 4. Module Implementation (Phase 2 — the 10 sub-systems)

Standard pattern per module: **Entity + controller with CRUD actions + Razor views (Index list w/ search & filters, Create/Edit form, Details) + role authorization + audit logging on writes.**

### 4.1 Pet & Owner Management
- **Controllers:** `OwnersController.cs`, `PetsController.cs`
- **Views:** `Views/Owners/` and `Views/Pets/` (Index, Create, Edit, Details)
- **Behavior:**
  - Owners = `Users` with Role "Pet Owner" (plus optional `ContactNumber`/`Address` fields — extend the entity; the data dictionary is minimal).
  - Pets CRUD with Owner dropdown; `MedicalHistory` text; owner sees only their pets (filter by `User.FindFirst(NameIdentifier)`).
- **Roles:** Admin/Staff full CRUD; Vet read; Owner read-own.
- **Done when:** staff can register a new owner + pet from the UI, and an owner logging in sees only their own pets.

### 4.2 Appointment Scheduling
- **Controller/Views:** `AppointmentsController.cs`, `Views/Appointments/`
- **Behavior:**
  - Owner books: pick pet, service, vet, date/time (form exists cosmetically on the landing page — move it into the authenticated owner portal and make it **save to `Appointments`** with Status = Pending).
  - Staff/Admin: full calendar/list, can create, reschedule, set Status (Pending/Completed/Cancelled).
  - Vet: sees only appointments where `VetId` = their id.
  - Validation: no double-booking per vet at the same time; no past-date booking.
- **Roles:** Owner create-own/read-own; Staff/Admin full; Vet read-own + status update.
- **Done when:** an owner booking appears instantly in the staff appointment list.

### 4.3 Veterinarian Management
- **Controller/Views:** `VeterinariansController.cs`, `Views/Veterinarians/`
- **Behavior:** list/create/edit/deactivate users with Role = Veterinarian; show specialty/availability (optional extra fields); show upcoming appointment count per vet.
- **Roles:** Admin full; Staff/Vet read.
- **Done when:** admin can onboard a vet account that can immediately log in.

### 4.4 Treatment Records
- **Controller/Views:** `TreatmentRecordsController.cs`, `Views/TreatmentRecords/`
- **Behavior:**
  - Vet opens a Completed appointment → creates record: `Diagnosis`, `Prescription`, `TreatmentNotes` (fields per data dictionary).
  - Auto-link `AppointmentId`; update the pet's `MedicalHistory` summary.
  - Owner sees treatment history in pet Details (read-own).
- **Roles:** Vet create/edit; Admin/Staff read; Owner read-own.
- **Done when:** a vet documents a consultation and the owner can view it on the pet's page.

### 4.5 Medicine Inventory
- **Controller/Views:** `InventoryController.cs`, `Views/Inventory/`
- **Behavior:**
  - CRUD items: name, category, quantity, unit price, reorder level, supplier (FK).
  - **Stock in/out actions** (increment/decrement with reason) so quantity changes are traceable.
  - **Low-stock highlight** where `Quantity <= ReorderLevel` + dashboard alert card.
- **Roles:** Admin/Staff full; Vet read (check availability before prescribing); Supplier read-own catalog.
- **Done when:** stock deductions appear and low-stock items are flagged.

### 4.6 Billing
- **Controller/Views:** `BillingController.cs`, `Views/Billing/`
- **Behavior:**
  - Staff creates invoice from a Completed appointment (pull owner via appointment → pet).
  - `TotalAmount`, `PaymentMethod` (Cash/Card/Online), `PaymentStatus` (Pending/Paid), `DateIssued`.
  - Owner portal: "My Invoices" + a Pay button.
  - **PayMongo integration** (doc's Billing API): create a Checkout Session via `https://api.paymongo.com/v1/checkout_sessions` (Basic auth with Base64 secret key); on successful payment redirect owner to invoice with status Paid. If no PayMongo key is available in time, ship the manual "mark as paid" flow first and wire PayMongo behind a flag (see §6).
- **Roles:** Staff/Admin create & mark paid; Owner read-own + pay-own.
- **Done when:** an invoice can be issued, paid (or marked paid), and seen by the owner.

### 4.7 Vaccination Reminders
- **Controller/Views:** `VaccinationRemindersController.cs`, `Views/VaccinationReminders/`
- **Behavior:**
  - Vet/Staff create reminders per pet: `VaccineName`, `DueDate`, Status Pending.
  - Owner sees upcoming reminders on their dashboard (and per pet).
  - **Notification job:** background service (`IHostedService`/`BackgroundService`) that runs daily, flips due reminders to Sent and sends email (SMTP — e.g., Gmail app password). The doc calls this the **Notification API**.
- **Roles:** Vet/Staff manage; Owner read-own.
- **Done when:** a reminder due today flips to Sent and the owner receives an email.

### 4.8 Customer CRM
- **Controller/Views:** `CrmController.cs`, `Views/Crm/`
- **Behavior:**
  - Staff/Admin log interactions (calls, follow-ups, visits) and record `Feedback` per owner.
  - `LoyaltyPoints`: award points per paid invoice (e.g., 1 pt per ₱100) and let owner redeem/view balance; show top customers.
  - Owner portal: submit feedback, view points.
- **Roles:** Staff/Admin manage; Owner read-own + submit feedback.
- **Done when:** feedback + loyalty points round-trip between owner and staff views.

### 4.9 Supplier Management
- **Controller/Views:** `SuppliersController.cs`, `Views/Suppliers/`
- **Behavior:**
  - Admin CRUD suppliers: name, contact, product catalog, contract details.
  - Supplier login sees **read-own** profile plus the inventory items linked via `Inventory.SupplierId`.
- **Roles:** Admin full; Staff read; Supplier read-own.
- **Done when:** a supplier can log in and see their catalog; admin can trace any inventory item back to its supplier.

### 4.10 Reports
- **Controller/Views:** `ReportsController.cs`, `Views/Reports/`
- **Behavior:**
  - Generate on demand: **Clinical** (appointments & treatments per vet/period), **Financial** (revenue from paid invoices, by method), **Operational** (inventory levels, reminder compliance).
  - Save each generated report to the `Reports` table (`ReportType`, `GeneratedBy`, `DateGenerated`, `Content`) per the data dictionary.
  - Present with **Chart.js** (`dotnet add package` not needed — CDN) + export to CSV; PDF export optional.
- **Roles:** Admin all; Vet clinical; Staff financial/operational.
- **Done when:** an admin generates a monthly financial report and it appears in report history.

---

## 5. Security Features (Phase 3 — required by the doc, currently absent)

| Doc requirement | Implementation to add |
|---|---|
| Role-based access | §3.4 policies + `[Authorize(Policy=...)]` on every module controller; verify each role gets 403 (not a leak) on others' pages |
| Two-factor authentication | For **Admin** (at minimum): email OTP — on login, generate 6-digit code, store with expiry, email via the same SMTP channel as reminders, show a "Verify code" step, then `SignInAsync`. Alternative: TOTP authenticator app if time allows |
| Encryption | Passwords hashed with BCrypt (§3.2); enforce HTTPS + `app.UseHsts()` already present for prod; SSL to SQL Server (`Encrypt=True`/`TrustServerCertificate`); consider encrypting `MedicalHistory` column with Data Protection APIs for the "encryption" screenshot |
| Audit logs | `AuditLog` table + `IAuditService` + an **action filter** that logs Create/Update/Delete on every module (user, action, entity, id, timestamp, before/after JSON). Admin-only `AuditLogsController` index with filters |
| Secure cloud storage | SQL Server accessible only over SSL with least-privilege credentials; host the DB with the doc's "Domain & Hosting with Cloud Integration" (Azure SQL / cloud VM SQL Server) with automated backups |
| Session management | Cookie auth config from §3.2: sliding 8h expiration, `HttpOnly`, `Secure` cookies, Logout action |
| Data privacy compliance | PH Data Privacy Act (RA 10173): consent checkbox at registration, flesh out the existing `Home/Privacy` page with what data is stored and why, data minimization note in docs |

---

## 6. External API Integrations (as listed in the doc)

| API (per doc) | Role in the system | Status / plan |
|---|---|---|
| **Firebase Authentication API** | Google Sign-In button already exists on the login page | Optional/stretch: wire Firebase Auth web SDK for Google SSO and upsert the user into `Users`. If skipped, remove or label the button — a dead button is worse than none. Cookie auth (§3.2) is the primary mechanism and satisfies the doc's auth requirement |
| **Appointment Scheduling API** | Booking flow | Implemented as `AppointmentsController` (§4.2) — present it as the system's internal scheduling API in the documentation |
| **PayMongo Billing API** | Online invoice payment | §4.6: Checkout Session + success redirect; store `paymentIntentId` on the invoice. Add a webhook endpoint only if you can expose a public URL (ngrok) during the demo |
| **Inventory Management API** | Stock CRUD + low-stock alerts | Implemented as `InventoryController` (§4.5) |
| **Notification API** | Vaccination reminders, invoice issued, appointment status changes | §4.7 background service + SMTP email sender service (`IEmailSender`) |
| **Reports API** | Report generation | Implemented as `ReportsController` (§4.10) |

---

## 7. Suggested Build Order (Milestones)

1. **M1 — Foundation:** DB context + all 10 entities + migration + seeder (§3.1, §3.3)
2. **M2 — Auth & RBAC:** real login/logout, cookies, roles, dashboard shell + role menus (§3.2–§3.5)
3. **M3 — Core clinic flow:** Pets & Owners → Appointments → Treatment Records (§4.1–§4.4) — this is the demo's backbone
4. **M4 — Business modules:** Inventory → Billing (manual) → Reminders → CRM → Suppliers (§4.5–§4.9)
5. **M5 — Insights & security polish:** Reports + charts, audit logs, 2FA for admin, PayMongo (§4.10, §5, §6)
6. **M6 — Deployment (doc: "Domain & Hosting with Cloud Integration"):** publish to a cloud host (Azure App Service or similar) + hosted SQL Server, custom domain + HTTPS, automated backups; put the deployed URL into the doc header
7. **M7 — Documentation support:** seed demo data, walk each role end-to-end, capture the screenshots the doc's Prototype section requires (one per transaction, labeled, with descriptions — both frontend and backend/source-code shots for the API/security sections)

Milestones 1–3 are the minimum viable system; M4–M5 complete the documented scope; M6–M7 finish the submission.

---

## 8. Acceptance Checklist (definition of "done")

- [ ] Each of the 5 doc accounts logs in and lands on a role-appropriate dashboard
- [ ] All 10 data-dictionary tables exist in SQL Server with the documented fields, enforced by EF entities
- [ ] Every module has list / create / edit / details flows with server-side validation
- [ ] A full clinic scenario works end-to-end: register owner → add pet → book appointment → staff confirms → vet completes + writes treatment → staff issues invoice → owner pays → points awarded → vaccination reminder email sent → admin generates report → audit log shows it all
- [ ] Unauthorized role access is blocked (403) on every module
- [ ] Audit trail records writes across modules; admin can view them
- [ ] Admin login requires the 2FA step
- [ ] At least one report renders a chart and is saved to the `Reports` table
- [ ] Login form no longer pre-fills the password, and the fake Google/forgot-password buttons are either wired or removed
