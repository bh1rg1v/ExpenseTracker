using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Income|Expense)$", ErrorMessage = "Type must be either 'Income' or 'Expense'.")]
        public string Type { get; set; } = "Expense"; // "Income" or "Expense"

        [Required]
        [StringLength(50)]
        public string Icon { get; set; } = "fa-tags"; // FontAwesome icon class name

        [Required]
        [StringLength(10)]
        public string Color { get; set; } = "#6c757d"; // HEX color representation (e.g. "#FF5733")
    }
}
