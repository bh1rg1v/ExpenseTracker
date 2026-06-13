using System;

namespace ExpenseTracker.ViewModels
{
    public class BudgetProgressViewModel
    {
        public int BudgetId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryIcon { get; set; } = string.Empty;
        public string CategoryColor { get; set; } = string.Empty;
        public decimal LimitAmount { get; set; }
        public decimal SpentAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public double ProgressPercentage
        {
            get
            {
                if (LimitAmount <= 0) return 0;
                var pct = (double)(SpentAmount / LimitAmount) * 100.0;
                return Math.Min(pct, 100.0); // Caps at 100 for visual progress bars
            }
        }

        public double ActualPercentage
        {
            get
            {
                if (LimitAmount <= 0) return 0;
                return (double)(SpentAmount / LimitAmount) * 100.0;
            }
        }

        public bool IsOverBudget => SpentAmount > LimitAmount;
        public bool IsNearLimit => SpentAmount >= LimitAmount * 0.9m && SpentAmount <= LimitAmount;
    }
}
