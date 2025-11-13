using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace WebApplication2.Controllers
{
    public class WalletsController : Controller
    {
        private readonly EstocksDbContext _context;

        public WalletsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: Wallets/Index
        public async Task<IActionResult> Index()
        {
            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Users");
            }

            // Get the current user's ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Users");
            }

            // Get user with wallet and stocks
            var user = await _context.Users
                .Include(u => u.Wallets)
                .Include(u => u.Stocks)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound();
            }

            // Get or create wallet for user
            var wallet = user.Wallets?.FirstOrDefault();
            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = userId,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            // Calculate portfolio values and stock quantities
            double totalInvestment = 0;
            double currentValue = 0;
            var stockQuantities = new Dictionary<int, int>();

            // Get all user's stocks
            var userStocks = await _context.Stocks
                .Where(s => s.UserId == userId)
                .ToListAsync();

            foreach (var stock in userStocks)
            {
                // Get total quantity owned (buy orders - sell orders)
                var buyQuantity = await _context.Orders
                    .Where(o => o.UserId == userId && o.StockId == stock.StockId && o.OrderType == "spot_buy")
                    .SumAsync(o => (int?)o.Quantity) ?? 0;

                var sellQuantity = await _context.Orders
                    .Where(o => o.UserId == userId && o.StockId == stock.StockId && o.OrderType == "spot_sell")
                    .SumAsync(o => (int?)o.Quantity) ?? 0;

                int ownedQuantity = buyQuantity - sellQuantity;
                stockQuantities[stock.StockId] = ownedQuantity;

                if (ownedQuantity > 0)
                {
                    // Calculate total investment (purchase price * quantity)
                    var buyOrders = await _context.Orders
                        .Where(o => o.UserId == userId && o.StockId == stock.StockId && o.OrderType == "spot_buy")
                        .ToListAsync();

                    foreach (var order in buyOrders)
                    {
                        totalInvestment += order.Price * order.Quantity;
                    }

                    // Subtract sell value from investment
                    var sellOrders = await _context.Orders
                        .Where(o => o.UserId == userId && o.StockId == stock.StockId && o.OrderType == "spot_sell")
                        .ToListAsync();

                    foreach (var order in sellOrders)
                    {
                        totalInvestment -= order.Price * order.Quantity;
                    }

                    // Calculate current value using current stock price
                    currentValue += stock.Price * ownedQuantity;
                }
            }

            // Calculate profit/loss
            double profitLoss = currentValue - totalInvestment;

            // Pass calculated values to view
            ViewBag.TotalInvestment = (int)totalInvestment;
            ViewBag.CurrentValue = (int)currentValue;
            ViewBag.ProfitLoss = (int)profitLoss;
            ViewBag.UserStocks = userStocks;
            ViewBag.StockQuantities = stockQuantities; // For modal details

            // Pass the wallet to the view
            return View(wallet);
        }



        // POST: Wallets/AddFunds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFunds(int amount)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Users");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Users");
            }

            if (amount <= 0)
            {
                TempData["Error"] = "Please enter a valid amount greater than 0.";
                return RedirectToAction("Index");
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = userId,
                    Balance = amount,
                    LastUpdated = DateTime.Now
                };
                _context.Wallets.Add(wallet);
            }
            else
            {
                wallet.Balance += amount;
                wallet.LastUpdated = DateTime.Now;
                _context.Wallets.Update(wallet);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully added ${amount:N0} to your wallet!";
            return RedirectToAction("Index");
        }
    }
}