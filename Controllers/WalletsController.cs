using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

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

            // Get user with wallet, stocks and banks
            var user = await _context.Users
                .Include(u => u.Wallets)
                .Include(u => u.Stocks)
                .Include(u => u.Banks) // include banks
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound();
            }

            // Provide user's banks to the view (may be empty)
            ViewBag.UserBanks = user.Banks?.ToList() ?? new List<Bank>();

            // Get or create wallet for user
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
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
            var ownedStocksList = new List<Stock>();

            // Get all user's stocks
            var allUserStocks = await _context.Stocks
                .Where(s => s.UserId == userId)
                .ToListAsync();

            foreach (var stock in allUserStocks)
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

                // Only include stocks with positive owned quantity
                if (ownedQuantity > 0)
                {
                    ownedStocksList.Add(stock);

                    // Calculate investment and current value
                    var buyOrders = await _context.Orders
                        .Where(o => o.UserId == userId && o.StockId == stock.StockId && o.OrderType == "spot_buy")
                        .ToListAsync();

                    foreach (var order in buyOrders)
                    {
                        totalInvestment += order.Price * order.Quantity;
                    }

                    var sellOrders = await _context.Orders
                        .Where(o => o.UserId == userId && o.StockId == stock.StockId && o.OrderType == "spot_sell")
                        .ToListAsync();

                    foreach (var order in sellOrders)
                    {
                        totalInvestment -= order.Price * order.Quantity;
                    }

                    currentValue += stock.Price * ownedQuantity;
                }
            }

            // Calculate profit/loss
            double profitLoss = currentValue - totalInvestment;

            // Get user's active future positions
            var futureTradings = await _context.FutureTradings
                .Include(ft => ft.Contract)
                    .ThenInclude(c => c.Stock)
                .Where(ft => ft.UserId == userId)
                .ToListAsync();

            // Calculate futures P&L
            double futuresMarginHeld = 0;
            double futuresCurrentValue = 0;

            foreach (var ft in futureTradings)
            {
                var marginPerContract = (int)(ft.Price * ft.Quantity * 0.15);
                futuresMarginHeld += marginPerContract;

                var currentStockPrice = ft.Contract?.Stock?.Price ?? ft.Price;

                if (ft.Contract?.ContractType == "LONG")
                {
                    futuresCurrentValue += (currentStockPrice - ft.Price) * ft.Quantity;
                }
                else // SHORT
                {
                    futuresCurrentValue += (ft.Price - currentStockPrice) * ft.Quantity;
                }
            }

            // Pass calculated values to view
            ViewBag.TotalInvestment = (int)totalInvestment;
            ViewBag.CurrentValue = (int)currentValue;
            ViewBag.ProfitLoss = (int)profitLoss;
            ViewBag.UserStocks = ownedStocksList;
            ViewBag.StockQuantities = stockQuantities;

            // Futures data
            ViewBag.FutureTradings = futureTradings;
            ViewBag.FuturesMarginHeld = (int)futuresMarginHeld;
            ViewBag.FuturesUnrealizedPL = (int)futuresCurrentValue;

            return View(wallet);
        }

        // POST: Wallets/AddFunds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFunds(int amount, int? bankId)
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

            // Ensure user has banks and selected a bank
            var userBanks = await _context.Banks.Where(b => b.UserId == userId).ToListAsync();
            if (userBanks == null || userBanks.Count == 0)
            {
                TempData["Error"] = "You must add a bank account before adding funds. Please add bank details first.";
                return RedirectToAction("Index");
            }

            if (!bankId.HasValue)
            {
                TempData["Error"] = "Please select a bank to add funds from.";
                return RedirectToAction("Index");
            }

            var bank = userBanks.FirstOrDefault(b => b.BankId == bankId.Value);
            if (bank == null)
            {
                TempData["Error"] = "Selected bank not found or does not belong to you.";
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

            // Optionally record a transaction referencing this add-funds event
            var tx = new Transaction
            {
                WalletId = wallet.WalletId,
                UserId = userId,
                StockId = null,
                Quantity = 0,
                TransactionType = "ADD_FUNDS"
            };
            _context.Transactions.Add(tx);

            await _context.SaveChangesAsync();

            // Mask account number display for privacy
            string acctDisplay = bank.AccountNumber.ToString();
            if (acctDisplay.Length > 4)
            {
                acctDisplay = "****" + acctDisplay.Substring(acctDisplay.Length - 4);
            }

            TempData["Success"] = $"Successfully added {amount:N0} PKR to your wallet from {bank.BankName} ({acctDisplay}).";
            return RedirectToAction("Index");
        }
    }
}