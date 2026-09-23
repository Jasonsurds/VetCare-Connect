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
public class PurchaseRequestsController : Controller
{
    private readonly VetCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly INotificationService _notif;

    public PurchaseRequestsController(VetCareDbContext db, IAuditService audit, INotificationService notif)
    {
        _db = db;
        _audit = audit;
        _notif = notif;
    }

    public async Task<IActionResult> Index(string? status, string? search)
    {
        var role = User.GetUserRole();
        if (role != "Supplier" && role != "Clinic Staff")
            return Forbid();

        var query = _db.PurchaseRequests
            .Include(r => r.InventoryItem).ThenInclude(i => i!.Supplier)
            .Include(r => r.Requester)
            .Include(r => r.Processor)
            .AsQueryable();

        if (role == "Supplier")
        {
            var supplierId = await GetSupplierIdAsync();
            query = query.Where(r => supplierId != null && r.InventoryItem!.SupplierID == supplierId);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.InventoryItem!.ItemName.Contains(search));

        var requests = await query
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Search = search;
        ViewData["Title"] = "Restock Requests";
        ViewData["DashTitle"] = role == "Supplier" ? "My Restock Requests" : "Restock Requests";
        return View(requests);
    }

    [Authorize(Roles = "Supplier, Administrator")]
    public async Task<IActionResult> Create(int? itemId, int? supplierId)
    {
        if (User.GetUserRole() == "Administrator")
        {
            if (supplierId == null) return NotFound();
            var supplier = await _db.Suppliers.FindAsync(supplierId);
            if (supplier == null) return NotFound();
            ViewBag.SupplierID = supplier.SupplierID;
            ViewBag.SupplierName = supplier.SupplierName;
            ViewBag.IsAdmin = true;
        }
        await PopulateItemDropdownAsync(itemId, supplierId);
        ViewData["Title"] = "New Restock Request";
        ViewData["DashTitle"] = "Request Restock";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Supplier, Administrator")]
    public async Task<IActionResult> Create(int? supplierId, int inventoryItemId, int quantity, string? notes)
    {
        var isAdmin = User.GetUserRole() == "Administrator";
        var item = await _db.InventoryItems.Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.ItemID == inventoryItemId);

        if (item == null)
        {
            ModelState.AddModelError(string.Empty, "Please select a valid item.");
        }
        else if (!isAdmin)
        {
            var sid = await GetSupplierIdAsync();
            if (sid == null)
            {
                ModelState.AddModelError(string.Empty, "No supplier profile is linked to this account. Please contact the clinic administrator.");
            }
            else if (item.SupplierID != sid)
            {
                ModelState.AddModelError(string.Empty, "Please select a valid item from your catalog.");
            }
        }
        else if (supplierId == null || item.SupplierID != supplierId)
        {
            ModelState.AddModelError(string.Empty, "Please select a valid item from this supplier's catalog.");
        }

        if (quantity < 1)
            ModelState.AddModelError(string.Empty, "Quantity must be at least 1.");

        if (item != null &&
            await _db.PurchaseRequests.AnyAsync(r => r.InventoryItemID == inventoryItemId && r.Status == "Pending"))
        {
            ModelState.AddModelError(string.Empty, $"There is already a pending restock request for '{item.ItemName}'. Wait for it to be received or cancel it first.");
        }

        if (!ModelState.IsValid)
        {
            if (isAdmin && supplierId != null)
            {
                var supplier = await _db.Suppliers.FindAsync(supplierId);
                if (supplier != null)
                {
                    ViewBag.SupplierID = supplier.SupplierID;
                    ViewBag.SupplierName = supplier.SupplierName;
                }
                ViewBag.IsAdmin = true;
            }
            await PopulateItemDropdownAsync(inventoryItemId, supplierId);
            return View();
        }

        var request = new PurchaseRequest
        {
            InventoryItemID = inventoryItemId,
            RequestedBy = User.GetUserId(),
            Quantity = quantity,
            Status = "Pending",
            Notes = notes,
            RequestedAt = DateTime.Now
        };
        _db.PurchaseRequests.Add(request);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create", "PurchaseRequests",
            $"{(isAdmin ? "Administrator" : "Supplier")} '{User.Identity?.Name}' requested restock for '{item!.ItemName}' (qty {quantity}).");

        var url = "/PurchaseRequests";
        if (isAdmin)
        {
            if (item.Supplier != null)
            {
                var supplierUser = await _db.Users.FirstOrDefaultAsync(u => u.Role == "Supplier" &&
                    (u.UserName == item.Supplier.SupplierName || u.Name == item.Supplier.SupplierName));
                if (supplierUser != null)
                {
                    await _notif.SendAsync(supplierUser.UserID, "New Restock Request 📦",
                        $"VetCare Connect requested {quantity} unit(s) of '{item.ItemName}' for restocking.", "General", url);
                }
            }
            await _notif.SendToRoleAsync("Clinic Staff", "New Restock Request 📦",
                $"Administrator '{User.Identity?.Name}' requested {quantity} unit(s) of '{item.ItemName}' restock from {item.Supplier?.SupplierName ?? "a supplier"}.", "General", url);
        }
        else
        {
            await _notif.SendToRoleAsync("Clinic Staff", "New Restock Request 📦",
                $"Supplier '{User.Identity?.Name}' requested {quantity} unit(s) of '{item.ItemName}'.", "General", url);
            await _notif.SendToRoleAsync("Administrator", "New Restock Request 📦",
                $"Supplier '{User.Identity?.Name}' requested {quantity} unit(s) of '{item.ItemName}'.", "General", url);
        }

        TempData["SuccessMessage"] = $"Restock request for '{item!.ItemName}' (qty {quantity}) has been submitted.";
        if (isAdmin) return RedirectToAction("Details", "Suppliers", new { id = supplierId });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Supplier")]
    public async Task<IActionResult> Cancel(int id)
    {
        var request = await _db.PurchaseRequests.Include(r => r.InventoryItem)
            .FirstOrDefaultAsync(r => r.RequestID == id);
        if (request == null) return NotFound();
        if (request.RequestedBy != User.GetUserId() || request.Status != "Pending")
            return Forbid();

        request.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "PurchaseRequests", $"Restock request #{id} for '{request.InventoryItem?.ItemName}' cancelled by requester.");
        TempData["SuccessMessage"] = "Restock request has been cancelled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Supplier")]
    public async Task<IActionResult> Confirm(int id)
    {
        var supplierId = await GetSupplierIdAsync();
        var request = await _db.PurchaseRequests
            .Include(r => r.InventoryItem)
            .Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.RequestID == id);
        if (request == null) return NotFound();
        if (request.Status != "Pending" || request.RequestedBy == User.GetUserId()
            || request.InventoryItem?.SupplierID == null || request.InventoryItem.SupplierID != supplierId)
        {
            return Forbid();
        }

        request.Status = "Confirmed";
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update", "PurchaseRequests",
            $"Restock request #{id} for '{request.InventoryItem.ItemName}' confirmed by supplier '{User.Identity?.Name}'.");

        var url = "/PurchaseRequests";
        await _notif.SendToRoleAsync("Clinic Staff", "Restock Request Confirmed ✅",
            $"Supplier '{User.Identity?.Name}' confirmed the restock request for '{request.InventoryItem.ItemName}' — please mark it received when the shipment arrives.", "General", url);
        await _notif.SendToRoleAsync("Administrator", "Restock Request Confirmed ✅",
            $"Supplier '{User.Identity?.Name}' confirmed the restock request for '{request.InventoryItem.ItemName}'.", "General", url);

        TempData["SuccessMessage"] = $"Restock request for '{request.InventoryItem.ItemName}' has been confirmed. The clinic can prepare the order.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Clinic Staff")]
    public async Task<IActionResult> MarkReceived(int id)
    {
        var request = await _db.PurchaseRequests
            .Include(r => r.InventoryItem)
            .Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.RequestID == id);
        if (request == null) return NotFound();
        if (request.Status != "Pending" && request.Status != "Confirmed")
        {
            TempData["SuccessMessage"] = "This restock request has already been processed.";
            return RedirectToAction(nameof(Index));
        }

        request.InventoryItem!.Quantity += request.Quantity;
        request.InventoryItem.LastUpdated = DateTime.Now;
        request.Status = "Received";
        request.ProcessedAt = DateTime.Now;
        request.ProcessedBy = User.GetUserId();
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update", "PurchaseRequests",
            $"Restock request #{id} for '{request.InventoryItem.ItemName}' marked as received (+{request.Quantity}); stock now {request.InventoryItem.Quantity}.");

        if (request.Requester != null)
        {
            await _notif.SendAsync(request.Requester.UserID, "Restock Request Received ✅",
                $"Your restock request for '{request.InventoryItem.ItemName}' was received. Stock is now {request.InventoryItem.Quantity}.",
                "General", "/PurchaseRequests");
        }

        TempData["SuccessMessage"] = $"'{request.InventoryItem.ItemName}' stock increased by {request.Quantity} (now {request.InventoryItem.Quantity}).";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateItemDropdownAsync(int? selected = null, int? supplierFor = null)
    {
        var isAdmin = User.GetUserRole() == "Administrator";

        var query = _db.InventoryItems.AsQueryable();
        if (isAdmin)
        {
            if (supplierFor == null)
            {
                ViewBag.InventoryItemID = new SelectList(new List<SelectListItem>());
                return;
            }
            query = query.Where(i => i.SupplierID == supplierFor);
        }
        else
        {
            var supplierId = await GetSupplierIdAsync();
            if (supplierId == null)
            {
                ViewBag.InventoryItemID = new SelectList(new List<SelectListItem>());
                return;
            }
            query = query.Where(i => i.SupplierID == supplierId);
        }

        var items = await query
            .OrderByDescending(i => i.Quantity <= i.ReorderLevel)
            .ThenBy(i => i.ItemName)
            .ToListAsync();

        ViewBag.InventoryItemID = new SelectList(
            items.Select(i => new
            {
                i.ItemID,
                Label = i.ItemName + " (" + i.Category + ") — in stock: " + i.Quantity
                    + (i.Quantity <= i.ReorderLevel ? " ⚠ reorder needed" : "")
            }),
            "ItemID", "Label", selected);
    }

    private async Task<int?> GetSupplierIdAsync()
    {
        var name = User.Identity?.Name ?? "";
        var me = await _db.Users.FirstOrDefaultAsync(u => u.UserName == name);
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s =>
            s.SupplierName == name || (me != null && s.SupplierName == me.Name));
        return supplier?.SupplierID;
    }
}