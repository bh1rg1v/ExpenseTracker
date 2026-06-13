using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Dashboard
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            // Fetch user transactions
            var userTransactions = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            // 1. Current Month Totals
            var currentMonthTransactions = await userTransactions
                .Where(t => t.Date >= startOfMonth && t.Date <= endOfMonth)
                .ToListAsync();

            decimal totalIncome = currentMonthTransactions
                .Where(t => t.Category?.Type == "Income")
                .Sum(t => t.Amount);

            decimal totalExpense = currentMonthTransactions
                .Where(t => t.Category?.Type == "Expense")
                .Sum(t => t.Amount);

            // 2. Recent Transactions (Last 5)
            var recentTransactions = await userTransactions
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.Id)
                .Take(5)
                .ToListAsync();

            // 3. Category Expenses Breakdown (Current Month)
            var categoryBreakdown = currentMonthTransactions
                .Where(t => t.Category?.Type == "Expense")
                .GroupBy(t => t.Category)
                .Select(g => new
                {
                    Name = g.Key?.Name ?? "Unknown",
                    Color = g.Key?.Color ?? "#6c757d",
                    Total = g.Sum(t => t.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var categoryLabels = categoryBreakdown.Select(x => x.Name).ToList();
            var categoryData = categoryBreakdown.Select(x => x.Total).ToList();
            var categoryColors = categoryBreakdown.Select(x => x.Color).ToList();

            // 4. Monthly Cash Flow (Last 6 Months)
            var monthlyLabels = new List<string>();
            var monthlyIncomeData = new List<decimal>();
            var monthlyExpenseData = new List<decimal>();

            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = today.AddMonths(-i);
                var monthStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var monthTransactions = await userTransactions
                    .Where(t => t.Date >= monthStart && t.Date <= monthEnd)
                    .ToListAsync();

                decimal inc = monthTransactions.Where(t => t.Category?.Type == "Income").Sum(t => t.Amount);
                decimal exp = monthTransactions.Where(t => t.Category?.Type == "Expense").Sum(t => t.Amount);

                monthlyLabels.Add(targetMonth.ToString("MMM yyyy"));
                monthlyIncomeData.Add(inc);
                monthlyExpenseData.Add(exp);
            }

            // 5. Active Budgets (Current Month)
            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.StartDate <= today && b.EndDate >= today)
                .ToListAsync();

            var budgetProgresses = new List<BudgetProgressViewModel>();
            foreach (var budget in budgets)
            {
                var spentAmounts = await userTransactions
                    .Where(t => t.CategoryId == budget.CategoryId && t.Date >= budget.StartDate && t.Date <= budget.EndDate)
                    .Select(t => t.Amount)
                    .ToListAsync();
                var spent = spentAmounts.Sum();

                budgetProgresses.Add(new BudgetProgressViewModel
                {
                    BudgetId = budget.Id,
                    CategoryName = budget.Category?.Name ?? "Unknown",
                    CategoryIcon = budget.Category?.Icon ?? "fa-tags",
                    CategoryColor = budget.Category?.Color ?? "#6c757d",
                    LimitAmount = budget.Amount,
                    SpentAmount = spent,
                    StartDate = budget.StartDate,
                    EndDate = budget.EndDate
                });
            }

            var viewModel = new DashboardViewModel
            {
                TotalIncome = totalIncome,
                TotalExpense = totalExpense,
                RecentTransactions = recentTransactions,
                Budgets = budgetProgresses,
                MonthlyLabels = monthlyLabels,
                MonthlyIncomeData = monthlyIncomeData,
                MonthlyExpenseData = monthlyExpenseData,
                CategoryLabels = categoryLabels,
                CategoryData = categoryData,
                CategoryColors = categoryColors
            };

            return View(viewModel);
        }
    }
}
