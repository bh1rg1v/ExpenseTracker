using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Data;
using ExpenseTracker.Models;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TransactionController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Transaction
        public async Task<IActionResult> Index(string? searchString, string? typeFilter, int? categoryFilter)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            // Filtering logic
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(t => t.Description != null && t.Description.ToLower().Contains(searchString.ToLower()));
            }

            if (!string.IsNullOrEmpty(typeFilter) && (typeFilter == "Income" || typeFilter == "Expense"))
            {
                query = query.Where(t => t.Category != null && t.Category.Type == typeFilter);
            }

            if (categoryFilter.HasValue)
            {
                query = query.Where(t => t.CategoryId == categoryFilter.Value);
            }

            var transactions = await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync();

            // Setup select lists for filters
            ViewData["Categories"] = new SelectList(await _context.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", categoryFilter);
            ViewData["SearchString"] = searchString;
            ViewData["TypeFilter"] = typeFilter;
            ViewData["CategoryFilter"] = categoryFilter;

            return View(transactions);
        }

        // GET: Transaction/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesDropDownList();
            return View(new Transaction { Date = DateTime.Today });
        }

        // POST: Transaction/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CategoryId,Amount,Date,Description")] Transaction transaction)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            transaction.UserId = userId;

            // Remove User navigation from state validation
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                _context.Add(transaction);
                await _context.SaveChangesAsync();

                // Check budget alert
                await CheckAndSetBudgetAlert(transaction);

                TempData["SuccessMessage"] = "Transaction added successfully!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateCategoriesDropDownList(transaction.CategoryId);
            return View(transaction);
        }

        // GET: Transaction/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (transaction == null) return NotFound();

            await PopulateCategoriesDropDownList(transaction.CategoryId);
            return View(transaction);
        }

        // POST: Transaction/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CategoryId,Amount,Date,Description")] Transaction transaction)
        {
            if (id != transaction.Id) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            transaction.UserId = userId;
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                try
                {
                    // Verify ownership
                    var original = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
                    if (original == null) return NotFound();

                    _context.Update(transaction);
                    await _context.SaveChangesAsync();

                    // Check budget alert
                    await CheckAndSetBudgetAlert(transaction);

                    TempData["SuccessMessage"] = "Transaction updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransactionExists(transaction.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateCategoriesDropDownList(transaction.CategoryId);
            return View(transaction);
        }

        // GET: Transaction/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var transaction = await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            
            if (transaction == null) return NotFound();

            return View(transaction);
        }

        // POST: Transaction/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Transaction deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TransactionExists(int id)
        {
            var userId = _userManager.GetUserId(User);
            return _context.Transactions.Any(e => e.Id == id && e.UserId == userId);
        }

        private async Task PopulateCategoriesDropDownList(object? selectedCategory = null)
        {
            var categoriesQuery = _context.Categories
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name);

            var categories = await categoriesQuery.ToListAsync();
            
            // Format dropdown items as "Category Name (Income)" or "Category Name (Expense)"
            var items = categories.Select(c => new
            {
                Id = c.Id,
                Name = $"{c.Name} ({c.Type})"
            });

            ViewData["CategoryId"] = new SelectList(items, "Id", "Name", selectedCategory);
        }

        private async Task CheckAndSetBudgetAlert(Transaction transaction)
        {
            // Fetch category type
            var category = await _context.Categories.FindAsync(transaction.CategoryId);
            if (category == null || category.Type != "Expense") return;

            // Fetch active budget for this category and user for the transaction's month
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.UserId == transaction.UserId && 
                                          b.CategoryId == transaction.CategoryId &&
                                          transaction.Date >= b.StartDate && 
                                          transaction.Date <= b.EndDate);

            if (budget == null) return;

            // Calculate total expenses for this category in the budget period
            var spentAmounts = await _context.Transactions
                .Where(t => t.UserId == transaction.UserId && 
                            t.CategoryId == transaction.CategoryId &&
                            t.Date >= budget.StartDate && 
                            t.Date <= budget.EndDate)
                .Select(t => t.Amount)
                .ToListAsync();
            var totalSpent = spentAmounts.Sum();

            if (totalSpent > budget.Amount)
            {
                TempData["BudgetWarning"] = $"Warning! You have exceeded your budget for '{category.Name}'. Total spent is ${totalSpent:N2} (Limit is ${budget.Amount:N2}).";
            }
            else if (totalSpent >= budget.Amount * 0.9m)
            {
                TempData["BudgetWarning"] = $"Notice: You have reached 90% of your budget for '{category.Name}'. Total spent is ${totalSpent:N2} (Limit is ${budget.Amount:N2}).";
            }
        }
    }
}
