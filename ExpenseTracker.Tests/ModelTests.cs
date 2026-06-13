using System;
using Xunit;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Tests
{
    public class ModelTests
    {
        [Fact]
        public void BudgetProgressViewModel_CalculatesProgressCorrectly()
        {
            // Arrange
            var vm = new BudgetProgressViewModel
            {
                LimitAmount = 100m,
                SpentAmount = 45m
            };

            // Act & Assert
            Assert.Equal(45.0, vm.ProgressPercentage);
            Assert.Equal(45.0, vm.ActualPercentage);
            Assert.False(vm.IsOverBudget);
            Assert.False(vm.IsNearLimit);
        }

        [Fact]
        public void BudgetProgressViewModel_NearLimit_TriggersFlag()
        {
            // Arrange
            var vm = new BudgetProgressViewModel
            {
                LimitAmount = 100m,
                SpentAmount = 95m
            };

            // Act & Assert
            Assert.Equal(95.0, vm.ProgressPercentage);
            Assert.True(vm.IsNearLimit);
            Assert.False(vm.IsOverBudget);
        }

        [Fact]
        public void BudgetProgressViewModel_OverBudget_CapsVisualProgress()
        {
            // Arrange
            var vm = new BudgetProgressViewModel
            {
                LimitAmount = 100m,
                SpentAmount = 120m
            };

            // Act & Assert
            Assert.Equal(100.0, vm.ProgressPercentage); // Capped for visual bars
            Assert.Equal(120.0, vm.ActualPercentage); // Uncapped actual
            Assert.True(vm.IsOverBudget);
            Assert.False(vm.IsNearLimit);
        }

        [Fact]
        public void BudgetProgressViewModel_ZeroLimit_ReturnsZero()
        {
            // Arrange
            var vm = new BudgetProgressViewModel
            {
                LimitAmount = 0m,
                SpentAmount = 50m
            };

            // Act & Assert
            Assert.Equal(0.0, vm.ProgressPercentage);
            Assert.Equal(0.0, vm.ActualPercentage);
            Assert.True(vm.IsOverBudget);
            Assert.False(vm.IsNearLimit);
        }
    }
}
