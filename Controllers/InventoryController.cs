using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VetCare.Data;
using VetCare.Helpers;
using VetCare.Models;
using VetCare.Services;

namespace VetCare.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;

    public InventoryController(VetCareDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private bool CanManage => User.GetUserRole() is "Administrator" or "Clinic Staff";

    public async Task<IActionResult> Index(string? search, string? filter)
    {
        var role = User.GetUserRole();
        var query = _db.InventoryItems.Include(i => i.Supplier).AsQueryable();

        if (role == "Supplier")
        {
            var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.SupplierName == User.Identity!.Name);
            query = query.Where(i => i.SupplierID == supplier!.SupplierID);
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => i.ItemName.Contains(search) || i.Category.Contains(search));

        if (filter == "low")
            query = query.Where(i => i.Quantity <= i.ReorderLevel);

        var items = await query.OrderBy(i => i.ItemName).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Filter = filter;
        ViewData["Title"] = "Medicine Inventory";
        ViewData["DashTitle"] = role == "Supplier" ? "My Catalog" : "Medicine Inventory";
        return View(items);
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Create()
    {
        await PopulateSupplierDropdownAsync();
        ViewData["DashTitle"] = "Add Inventory Item";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSupplierDropdownAsync(item.SupplierID);
            return View(item);
        }
        item.LastUpdated = DateTime.Now;
        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Inventory", $"Added item '{item.ItemName}' (qty {item.Quantity}).");
        TempData["SuccessMessage"] = $"'{item.ItemName}' has been added to the inventory.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var item = await _db.InventoryItems.FindAsync(id);
        if (item == null) return NotFound();
        await PopulateSupplierDropdownAsync(item.SupplierID);
        ViewData["DashTitle"] = $"Edit item — {item.ItemName}";
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Edit(int id, InventoryItem item)
    {
        if (id != item.ItemID) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateSupplierDropdownAsync(item.SupplierID);
            return View(item);
        }
        item.LastUpdated = DateTime.Now;
        _db.Update(item);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Inventory", $"Updated item '{item.ItemName}' (ID {id}).");
        TempData["SuccessMessage"] = $"'{item.ItemName}' has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> AdjustStock(int id, int change, string? reason)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item == null) return NotFound();

        var newQty = item.Quantity + change;
        if (newQty < 0)
        {
            TempData["SuccessMessage"] = $"Cannot deduct {Math.Abs(change)} — only {item.Quantity} in stock.";
            return RedirectToAction(nameof(Index));
        }

        item.Quantity = newQty;
        item.LastUpdated = DateTime.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Inventory",
            $"Stock adjusted for '{item.ItemName}' (ID {id}): {(change >= 0 ? "+" : "")}{change} → {newQty} in stock.{(string.IsNullOrWhiteSpace(reason) ? "" : " Reason: " + reason)}");
        TempData["SuccessMessage"] = $"Stock for '{item.ItemName}' adjusted: {(change >= 0 ? "+" : "")}{change} (now {newQty}).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator, Clinic Staff")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.InventoryItems.FindAsync(id);
        if (item != null)
        {
            _db.InventoryItems.Remove(item);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Delete", "Inventory", $"Deleted item '{item.ItemName}' (ID {id}).");
            TempData["SuccessMessage"] = $"'{item.ItemName}' has been removed from the inventory.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSupplierDropdownAsync(int? selected = null)
    {
        var suppliers = await _db.Suppliers.OrderBy(s => s.SupplierName)
            .Select(s => new { s.SupplierID, s.SupplierName }).ToListAsync();
        ViewBag.SupplierID = new SelectList(suppliers, "SupplierID", "SupplierName", selected);
    }
}
