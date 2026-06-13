using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class BudgetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BudgetController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Budget
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.StartDate)
                .ToListAsync();

            var progressList = new List<BudgetProgressViewModel>();

            foreach (var budget in budgets)
            {
                // Sum expenses in this category for this user during the budget duration
                var spentAmounts = await _context.Transactions
                    .Where(t => t.UserId == userId && 
                                t.CategoryId == budget.CategoryId && 
                                t.Date >= budget.StartDate && 
                                t.Date <= budget.EndDate)
                    .Select(t => t.Amount)
                    .ToListAsync();
                var totalSpent = spentAmounts.Sum();

                progressList.Add(new BudgetProgressViewModel
                {
                    BudgetId = budget.Id,
                    CategoryName = budget.Category?.Name ?? "Unknown",
                    CategoryIcon = budget.Category?.Icon ?? "fa-tags",
                    CategoryColor = budget.Category?.Color ?? "#6c757d",
                    LimitAmount = budget.Amount,
                    SpentAmount = totalSpent,
                    StartDate = budget.StartDate,
                    EndDate = budget.EndDate
                });
            }

            return View(progressList);
        }

        // GET: Budget/Create
        public async Task<IActionResult> Create()
        {
            await PopulateExpenseCategoriesDropDownList();
            return View(new Budget
            {
                StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                EndDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1)
            });
        }

        // POST: Budget/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CategoryId,Amount,StartDate,EndDate")] Budget budget)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            budget.UserId = userId;
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            // Check if user already has a budget for this category overlapping this date range
            var exists = await _context.Budgets.AnyAsync(b => b.UserId == userId &&
                                                             b.CategoryId == budget.CategoryId &&
                                                             ((budget.StartDate >= b.StartDate && budget.StartDate <= b.EndDate) ||
                                                              (budget.EndDate >= b.StartDate && budget.EndDate <= b.EndDate)));
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "An active budget already overlaps with the selected category and date range.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(budget);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Budget category created successfully!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateExpenseCategoriesDropDownList(budget.CategoryId);
            return View(budget);
        }

        // GET: Budget/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget == null) return NotFound();

            await PopulateExpenseCategoriesDropDownList(budget.CategoryId);
            return View(budget);
        }

        // POST: Budget/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CategoryId,Amount,StartDate,EndDate")] Budget budget)
        {
            if (id != budget.Id) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            budget.UserId = userId;
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            // Check overlap excluding this specific budget
            var exists = await _context.Budgets.AnyAsync(b => b.Id != id &&
                                                             b.UserId == userId &&
                                                             b.CategoryId == budget.CategoryId &&
                                                             ((budget.StartDate >= b.StartDate && budget.StartDate <= b.EndDate) ||
                                                              (budget.EndDate >= b.StartDate && budget.EndDate <= b.EndDate)));
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "An active budget already overlaps with the selected category and date range.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Verify ownership
                    var original = await _context.Budgets.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
                    if (original == null) return NotFound();

                    _context.Update(budget);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Budget updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BudgetExists(budget.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateExpenseCategoriesDropDownList(budget.CategoryId);
            return View(budget);
        }

        // GET: Budget/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var budget = await _context.Budgets
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            
            if (budget == null) return NotFound();

            return View(budget);
        }

        // POST: Budget/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (budget != null)
            {
                _context.Budgets.Remove(budget);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Budget limits deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool BudgetExists(int id)
        {
            var userId = _userManager.GetUserId(User);
            return _context.Budgets.Any(e => e.Id == id && e.UserId == userId);
        }

        private async Task PopulateExpenseCategoriesDropDownList(object? selectedCategory = null)
        {
            // Only fetch Expense categories since budgets apply to spending limits
            var categories = await _context.Categories
                .Where(c => c.Type == "Expense")
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", selectedCategory);
        }
    }
}
