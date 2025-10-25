﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace WebApplication2.Controllers
{
    public class StocksController : Controller
    {
        private readonly EstocksDbContext _context;
        private readonly StockDataService _stockDataService;

        public StocksController(EstocksDbContext context, StockDataService stockDataService)
        {
            _context = context;
            _stockDataService = stockDataService;
        }

        // GET: /Stocks - Display all stocks
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get all available PSX stocks
                var availableStocks = _stockDataService.GetAvailableStocks();
                var symbols = availableStocks.Keys.ToList();

                // Fetch live quotes (will use fallback data if API fails)
                var liveQuotes = await _stockDataService.GetMultipleStockQuotes(symbols);

                if (liveQuotes == null || !liveQuotes.Any())
                {
                    ViewBag.Message = "Unable to fetch stock data. Displaying sample data.";
                }

                return View(liveQuotes);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error loading stocks: {ex.Message}";
                return View(new List<StockQuote>());
            }
        }

        // GET: /Stocks/Details/OGDC
        public async Task<IActionResult> Details(string symbol, string period = "1mo")
        {
            if (string.IsNullOrEmpty(symbol))
                return NotFound();

            // Get stock quote
            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
                return NotFound();

            // Calculate change
            quote.Change = quote.CurrentPrice - quote.PreviousClose;
            quote.ChangePercent = quote.PreviousClose != 0
                ? (quote.Change / quote.PreviousClose) * 100
                : 0;

            // Get historical data for chart with selected period
            var historicalData = await _stockDataService.GetHistoricalData(symbol, period);

            // Get user's wallet if authenticated
            Wallet? userWallet = null;
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    userWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                }
            }

            ViewBag.HistoricalData = historicalData;
            ViewBag.UserWallet = userWallet;
            ViewBag.SelectedPeriod = period; // Pass selected period to view

            return View(quote);
        }

        // POST: /Stocks/BuySpot
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuySpot(string symbol, int quantity)
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Users");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            if (quantity <= 0)
            {
                TempData["Error"] = "Invalid quantity. Please enter a positive number.";
                return RedirectToAction("Details", new { symbol });
            }

            // Get current stock price
            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
            {
                TempData["Error"] = "Unable to fetch stock price. Please try again.";
                return RedirectToAction("Details", new { symbol });
            }

            int totalCost = (int)(quote.CurrentPrice * quantity);

            // Get user's wallet
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null || wallet.Balance < totalCost)
            {
                TempData["Error"] = $"Insufficient balance. Required: ${totalCost:N0}, Available: ${wallet?.Balance ?? 0:N0}";
                return RedirectToAction("Details", new { symbol });
            }

            // Deduct from wallet
            wallet.Balance -= totalCost;
            wallet.LastUpdated = DateTime.Now;

            // Create or update stock ownership
            var existingStock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.CompanyName == quote.CompanyName && s.UserId == userId);

            if (existingStock != null)
            {
                // Update existing stock - this is simplified, you might want to track individual purchases
                existingStock.Price = (int)quote.CurrentPrice;
            }
            else
            {
                // Create new stock entry
                var newStock = new Stock
                {
                    CompanyName = quote.CompanyName,
                    Price = (int)quote.CurrentPrice,
                    UserId = userId
                };
                _context.Stocks.Add(newStock);
            }

            // Create spot trading record
            var spotTrade = new SpotTrading
            {
                UserId = userId,
                StockId = existingStock?.StockId,
                Quantity = quantity,
                Price = (int)quote.CurrentPrice,
                TradeTime = DateTime.Now
            };
            _context.SpotTradings.Add(spotTrade);

            // Create transaction record
            var transaction = new Transaction
            {
                WalletId = wallet.WalletId,
                UserId = userId,
                StockId = existingStock?.StockId,
                Quantity = quantity,
                TransactionType = "BUY_SPOT"
            };
            _context.Transactions.Add(transaction);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully purchased {quantity} shares of {quote.CompanyName} for ${totalCost:N0}";
            return RedirectToAction("Details", new { symbol });
        }

        // POST: /Stocks/SellSpot
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SellSpot(string symbol, int quantity)
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Users");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            if (quantity <= 0)
            {
                TempData["Error"] = "Invalid quantity. Please enter a positive number.";
                return RedirectToAction("Details", new { symbol });
            }

            // Get current stock price
            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
            {
                TempData["Error"] = "Unable to fetch stock price. Please try again.";
                return RedirectToAction("Details", new { symbol });
            }

            // Check if user owns this stock
            var ownedStock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.CompanyName == quote.CompanyName && s.UserId == userId);

            // Note: You should track quantity owned. For now, we'll just check if they own it
            if (ownedStock == null)
            {
                TempData["Error"] = "You don't own this stock.";
                return RedirectToAction("Details", new { symbol });
            }

            int totalRevenue = (int)(quote.CurrentPrice * quantity);

            // Get user's wallet
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                TempData["Error"] = "Wallet not found.";
                return RedirectToAction("Details", new { symbol });
            }

            // Add to wallet
            wallet.Balance += totalRevenue;
            wallet.LastUpdated = DateTime.Now;

            // Create spot trading record
            var spotTrade = new SpotTrading
            {
                UserId = userId,
                StockId = ownedStock.StockId,
                Quantity = quantity,
                Price = (int)quote.CurrentPrice,
                TradeTime = DateTime.Now
            };
            _context.SpotTradings.Add(spotTrade);

            // Create transaction record
            var transaction = new Transaction
            {
                WalletId = wallet.WalletId,
                UserId = userId,
                StockId = ownedStock.StockId,
                Quantity = quantity,
                TransactionType = "SELL_SPOT"
            };
            _context.Transactions.Add(transaction);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully sold {quantity} shares of {quote.CompanyName} for ${totalRevenue:N0}";
            return RedirectToAction("Details", new { symbol });
        }

        // POST: /Stocks/BuyFuture
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyFuture(string symbol, int quantity, DateTime expiryDate, string contractType)
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Users");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            // Get current stock price
            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
            {
                TempData["Error"] = "Unable to fetch stock price.";
                return RedirectToAction("Details", new { symbol });
            }

            int contractPrice = (int)quote.CurrentPrice;
            int totalCost = contractPrice * quantity;

            // Get user's wallet
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null || wallet.Balance < totalCost)
            {
                TempData["Error"] = $"Insufficient balance for future contract.";
                return RedirectToAction("Details", new { symbol });
            }

            // Deduct from wallet
            wallet.Balance -= totalCost;
            wallet.LastUpdated = DateTime.Now;

            // Get or create stock
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.CompanyName == quote.CompanyName && s.UserId == userId);

            if (stock == null)
            {
                stock = new Stock
                {
                    CompanyName = quote.CompanyName,
                    Price = contractPrice,
                    UserId = userId
                };
                _context.Stocks.Add(stock);
                await _context.SaveChangesAsync();
            }

            // Create future contract
            var futureContract = new FutureContract
            {
                StockId = stock.StockId,
                ExpiryDate = expiryDate,
                ContractPrice = contractPrice,
                ContractType = contractType
            };
            _context.FutureContracts.Add(futureContract);
            await _context.SaveChangesAsync();

            // Create future trading record
            var futureTrade = new FutureTrading
            {
                ContractId = futureContract.ContractId,
                UserId = userId,
                Quantity = quantity,
                Price = contractPrice,
                TradeTime = DateTime.Now
            };
            _context.FutureTradings.Add(futureTrade);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Future contract created for {quantity} shares of {quote.CompanyName}";
            return RedirectToAction("Details", new { symbol });
        }
    }
}