using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;
using VetCare.Models;
using VetCare.Services;

namespace VetCare.Controllers;

[Authorize]
public class SuppliersController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public SuppliersController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var suppliers = await _db.Suppliers
            .Include(s => s.InventoryItems)
            .OrderBy(s => s.SupplierName)
            .ToListAsync();
        ViewData["Title"] = "Suppliers";
        ViewData["DashTitle"] = "Supplier Management";
        return View(suppliers);
    }

    [Authorize(Roles = "Supplier")]
    public async Task<IActionResult> My()
    {
        var supplier = await _db.Suppliers
            .Include(s => s.InventoryItems)
            .FirstOrDefaultAsync(s => s.SupplierName == User.Identity!.Name);
        if (supplier == null) return NotFound();
        ViewData["Title"] = "My Profile";
        ViewData["DashTitle"] = "My Supplier Profile";
        return View("Details", supplier);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var supplier = await _db.Suppliers
            .Include(s => s.InventoryItems)
            .FirstOrDefaultAsync(s => s.SupplierID == id);
        if (supplier == null) return NotFound();
        ViewData["Title"] = supplier.SupplierName;
        ViewData["DashTitle"] = $"Supplier — {supplier.SupplierName}";
        return View(supplier);
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Create()
    {
        ViewData["DashTitle"] = "Add Supplier";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        if (!ModelState.IsValid) return View(supplier);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Suppliers", $"Added supplier '{supplier.SupplierName}'.");
        TempData["SuccessMessage"] = $"Supplier '{supplier.SupplierName}' has been added.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier == null) return NotFound();
        ViewData["DashTitle"] = $"Edit supplier — {supplier.SupplierName}";
        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id, Supplier supplier)
    {
        if (id != supplier.SupplierID) return NotFound();
        if (!ModelState.IsValid) return View(supplier);
        _db.Update(supplier);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Suppliers", $"Updated supplier '{supplier.SupplierName}'.");
        TempData["SuccessMessage"] = $"Supplier '{supplier.SupplierName}' has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _db.Suppliers.Include(s => s.InventoryItems).FirstOrDefaultAsync(s => s.SupplierID == id);
        if (supplier != null)
        {
            if (supplier.InventoryItems.Any())
            {
                TempData["SuccessMessage"] = $"Cannot delete '{supplier.SupplierName}' — {supplier.InventoryItems.Count} inventory item(s) reference it. Reassign them first.";
                return RedirectToAction(nameof(Index));
            }
            _db.Suppliers.Remove(supplier);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "Suppliers", $"Deleted supplier '{supplier.SupplierName}'.");
            TempData["SuccessMessage"] = $"Supplier '{supplier.SupplierName}' has been deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}
