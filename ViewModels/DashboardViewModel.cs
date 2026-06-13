using System.Collections.Generic;
using ExpenseTracker.Models;

namespace ExpenseTracker.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetBalance => TotalIncome - TotalExpense;

        public List<Transaction> RecentTransactions { get; set; } = new();
        public List<BudgetProgressViewModel> Budgets { get; set; } = new();

        // Chart 1: Monthly Income vs Expenses (Last 6 Months)
        public List<string> MonthlyLabels { get; set; } = new();
        public List<decimal> MonthlyIncomeData { get; set; } = new();
        public List<decimal> MonthlyExpenseData { get; set; } = new();

        // Chart 2: Category Breakdown (Current Month)
        public List<string> CategoryLabels { get; set; } = new();
        public List<decimal> CategoryData { get; set; } = new();
        public List<string> CategoryColors { get; set; } = new();
    }
}
