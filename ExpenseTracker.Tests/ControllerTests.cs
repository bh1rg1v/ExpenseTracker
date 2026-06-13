using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using ExpenseTracker.Controllers;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Tests
{
    public class ControllerTests
    {
        private DbContextOptions<ApplicationDbContext> CreateNewInMemoryDatabaseOptions()
        {
            // Create a unique database name per test to avoid interference
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "ExpenseTrackerTest_" + Guid.NewGuid().ToString())
                .Options;
        }

        private Mock<UserManager<ApplicationUser>> CreateMockUserManager(string returnedUserId = "test-user-id")
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var mock = new Mock<UserManager<ApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
            
            mock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(returnedUserId);
            return mock;
        }

        private void SetControllerUserContext(Controller controller)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimTypes.Name, "dev@example.com")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Setup TempData to avoid NullReferenceException during controller action redirects/alerts
            var tempDataProvider = new Mock<ITempDataProvider>();
            controller.TempData = new TempDataDictionary(controller.HttpContext, tempDataProvider.Object);
        }

        [Fact]
        public async Task DashboardController_Index_CalculatesSummaryMetricsCorrectly()
        {
            // Arrange
            var options = CreateNewInMemoryDatabaseOptions();
            var mockUserManager = CreateMockUserManager();

            using (var context = new ApplicationDbContext(options))
            {
                // Seed categories
                var categoryIncome = new Category { Id = 1, Name = "Salary", Type = "Income", Icon = "fa-wallet", Color = "#2ec4b6" };
                var categoryExpense = new Category { Id = 2, Name = "Groceries", Type = "Expense", Icon = "fa-shopping-basket", Color = "#f4a261" };
                context.Categories.AddRange(categoryIncome, categoryExpense);

                // Seed transactions (Current Month)
                var today = DateTime.Today;
                var t1 = new Transaction { Id = 1, UserId = "test-user-id", CategoryId = 1, Amount = 2000m, Date = today, Description = "Salary" };
                var t2 = new Transaction { Id = 2, UserId = "test-user-id", CategoryId = 2, Amount = 150m, Date = today, Description = "Groceries" };
                // Another user's transaction (should be ignored)
                var t3 = new Transaction { Id = 3, UserId = "other-user-id", CategoryId = 2, Amount = 50m, Date = today, Description = "Other Grocery" };
                
                context.Transactions.AddRange(t1, t2, t3);
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var controller = new DashboardController(context, mockUserManager.Object);
                SetControllerUserContext(controller);

                // Act
                var result = await controller.Index();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<DashboardViewModel>(viewResult.Model);

                Assert.Equal(2000m, model.TotalIncome);
                Assert.Equal(150m, model.TotalExpense);
                Assert.Equal(1850m, model.NetBalance);
                Assert.Single(model.CategoryLabels);
                Assert.Equal("Groceries", model.CategoryLabels[0]);
                Assert.Equal(150m, model.CategoryData[0]);
                Assert.Equal(2, model.RecentTransactions.Count); // Only current test user's transactions
            }
        }

        [Fact]
        public async Task BudgetController_Index_AggregatesExpenditureWithinRange()
        {
            // Arrange
            var options = CreateNewInMemoryDatabaseOptions();
            var mockUserManager = CreateMockUserManager();

            using (var context = new ApplicationDbContext(options))
            {
                var cat = new Category { Id = 1, Name = "Utilities", Type = "Expense", Icon = "fa-bolt", Color = "#457b9d" };
                context.Categories.Add(cat);

                // Set a budget for the current month
                var budget = new Budget
                {
                    Id = 1,
                    UserId = "test-user-id",
                    CategoryId = 1,
                    Amount = 100m,
                    StartDate = new DateTime(2026, 6, 1),
                    EndDate = new DateTime(2026, 6, 30)
                };
                context.Budgets.Add(budget);

                // Seed Transactions:
                // 1. Within range ($40)
                context.Transactions.Add(new Transaction { Id = 1, UserId = "test-user-id", CategoryId = 1, Amount = 40m, Date = new DateTime(2026, 6, 15) });
                // 2. Within range ($25)
                context.Transactions.Add(new Transaction { Id = 2, UserId = "test-user-id", CategoryId = 1, Amount = 25m, Date = new DateTime(2026, 6, 20) });
                // 3. Out of range ($80, should be ignored for this budget month)
                context.Transactions.Add(new Transaction { Id = 3, UserId = "test-user-id", CategoryId = 1, Amount = 80m, Date = new DateTime(2026, 7, 5) });
                
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var controller = new BudgetController(context, mockUserManager.Object);
                SetControllerUserContext(controller);

                // Act
                var result = await controller.Index();

                // Assert
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<List<BudgetProgressViewModel>>(viewResult.Model);

                Assert.Single(model);
                var budgetProgress = model[0];
                Assert.Equal(100m, budgetProgress.LimitAmount);
                Assert.Equal(65m, budgetProgress.SpentAmount); // $40 + $25
                Assert.Equal(35m, budgetProgress.LimitAmount - budgetProgress.SpentAmount); // Remaining
                Assert.Equal(65.0, budgetProgress.ProgressPercentage);
                Assert.False(budgetProgress.IsOverBudget);
            }
        }

        [Fact]
        public async Task TransactionController_Create_InsertsRecordAndChecksBudgetAlert()
        {
            // Arrange
            var options = CreateNewInMemoryDatabaseOptions();
            var mockUserManager = CreateMockUserManager();

            using (var context = new ApplicationDbContext(options))
            {
                var cat = new Category { Id = 1, Name = "Housing", Type = "Expense", Icon = "fa-home", Color = "#e63946" };
                context.Categories.Add(cat);
                
                // Add Budget limit of $500
                context.Budgets.Add(new Budget
                {
                    Id = 1,
                    UserId = "test-user-id",
                    CategoryId = 1,
                    Amount = 500m,
                    StartDate = new DateTime(2026, 6, 1),
                    EndDate = new DateTime(2026, 6, 30)
                });
                
                await context.SaveChangesAsync();
            }

            using (var context = new ApplicationDbContext(options))
            {
                var controller = new TransactionController(context, mockUserManager.Object);
                SetControllerUserContext(controller);

                var newTransaction = new Transaction
                {
                    CategoryId = 1,
                    Amount = 550m, // Exceeds the budget limit of $500!
                    Date = new DateTime(2026, 6, 10),
                    Description = "June Rent"
                };

                // Act
                var result = await controller.Create(newTransaction);

                // Assert
                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);

                // Verify transaction is inserted
                var dbTransaction = await context.Transactions.FirstOrDefaultAsync(t => t.Description == "June Rent");
                Assert.NotNull(dbTransaction);
                Assert.Equal(550m, dbTransaction.Amount);
                Assert.Equal("test-user-id", dbTransaction.UserId);

                // Verify budget warning alert was triggered
                Assert.NotNull(controller.TempData["BudgetWarning"]);
                Assert.Contains("exceeded your budget", controller.TempData["BudgetWarning"]!.ToString());
            }
        }
    }
}
