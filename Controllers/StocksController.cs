using Microsoft.AspNetCore.Mvc;
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

            // Get user's owned quantity for this stock if authenticated
            int ownedQuantity = 0;
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    var ownedStock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.CompanyName == quote.CompanyName && s.UserId == userId);

                    if (ownedStock != null)
                    {
                        ownedQuantity = await _context.Transactions
                            .Where(t => t.UserId == userId && t.StockId == ownedStock.StockId)
                            .SumAsync(t => t.TransactionType == "BUY_SPOT" ? t.Quantity : -t.Quantity);
                    }
                }
            }

            ViewBag.OwnedQuantity = ownedQuantity;


            return View(quote);
        }

        // GET: /Stocks/Portfolio - Display user's stock holdings
        public async Task<IActionResult> Portfolio()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Users");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            // Get all stocks owned by user with quantities
            var portfolio = await _context.Transactions
                .Where(t => t.UserId == userId)
                .GroupBy(t => new { t.StockId, t.Stock.CompanyName })
                .Select(g => new
                {
                    StockId = g.Key.StockId,
                    CompanyName = g.Key.CompanyName,
                    Quantity = g.Sum(t => t.TransactionType == "BUY_SPOT" ? t.Quantity : -t.Quantity)
                })
                .Where(p => p.Quantity > 0) // Only show stocks with positive quantity
                .ToListAsync();

            return View(portfolio);
        }


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

            // Get or create stock record that's associated with this user
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.CompanyName == quote.CompanyName && s.UserId == userId);

            if (stock != null)
            {
                // Update price
                stock.Price = (int)quote.CurrentPrice;
            }
            else
            {
                // Create new stock entry and save it right away so we get StockId
                stock = new Stock
                {
                    CompanyName = quote.CompanyName,
                    Price = (int)quote.CurrentPrice,
                    UserId = userId
                };
                _context.Stocks.Add(stock);
                await _context.SaveChangesAsync(); // ensure stock.StockId is populated
            }

            try
            {
                // Create spot trading record
                var spotTrade = new SpotTrading
                {
                    UserId = userId,
                    StockId = stock.StockId,
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
                    StockId = stock.StockId,
                    Quantity = quantity,
                    TransactionType = "BUY_SPOT"
                };
                _context.Transactions.Add(transaction);

                // Create an Order record for this buy
                var order = new Order
                {
                    UserId = userId,
                    StockId = stock.StockId,
                    OrderType = "spot_buy",
                    Quantity = quantity,
                    Price = (int)quote.CurrentPrice,
                    OrderStatus = true
                };
                _context.Orders.Add(order);

                // Debug/log: enumerate entries that will be saved (useful during debugging)
                var entries = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                    .Select(e => new { e.Entity.GetType().Name, e.State })
                    .ToList();

                // Optionally: put this info into TempData for quick local debugging (remove in prod)
                TempData["SavePreview"] = System.Text.Json.JsonSerializer.Serialize(entries);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully purchased {quantity} shares of {quote.CompanyName} for ${totalCost:N0}";
            }
            catch (Exception ex)
            {
                // surface the exception message so you can see what's failing
                TempData["Error"] = "Failed to complete purchase: " + ex.Message;

                // Optionally log the full exception to console or a logger
                Console.WriteLine(ex); // replace with your logger if you have one
            }

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

            if (ownedStock == null)
            {
                TempData["Error"] = "You don't own this stock.";
                return RedirectToAction("Details", new { symbol });
            }

            // Calculate actual owned quantity from transactions
            var ownedQuantity = await _context.Transactions
                .Where(t => t.UserId == userId && t.StockId == ownedStock.StockId)
                .SumAsync(t => t.TransactionType == "BUY_SPOT" ? t.Quantity : -t.Quantity);

            // Validate user has enough shares to sell
            if (ownedQuantity < quantity)
            {
                TempData["Error"] = $"Insufficient shares. You own {ownedQuantity} shares but tried to sell {quantity}.";
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

            try
            {
                // Add to wallet
                wallet.Balance += totalRevenue;
                wallet.LastUpdated = DateTime.Now;

                // Create spot trading record with negative quantity to indicate sell
                var spotTrade = new SpotTrading
                {
                    UserId = userId,
                    StockId = ownedStock.StockId,
                    Quantity = -quantity, // Negative to indicate sell
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

                // Create an Order record for this sell
                var order = new Order
                {
                    UserId = userId,
                    StockId = ownedStock.StockId,
                    OrderType = "spot_sell",
                    Quantity = quantity,
                    Price = (int)quote.CurrentPrice,
                    OrderStatus = false
                };
                _context.Orders.Add(order);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully sold {quantity} shares of {quote.CompanyName} for ${totalRevenue:N0}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to complete sale: " + ex.Message;
                Console.WriteLine(ex);
            }

            return RedirectToAction("Details", new { symbol });
        }


        // POST: /Stocks/BuyFuture
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Replace the BuyFuture method in StocksController.cs (around line 375)
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
                TempData["Error"] = $"Insufficient balance. Required: {totalCost} PKR, Available: {wallet?.Balance ?? 0} PKR";
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

            try
            {
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

                // Create Order - THIS WAS MISSING!
                string orderType = contractType == "LONG" ? "future_long" : "future_short";
                var order = new Order
                {
                    UserId = userId,
                    StockId = stock.StockId,
                    OrderType = orderType,
                    Quantity = quantity,
                    Price = contractPrice,
                    OrderStatus = true
                };
                _context.Orders.Add(order);

                // Create Transaction - THIS WAS MISSING!
                string transactionType = contractType == "LONG" ? "BUY_FUTURE_LONG" : "SELL_FUTURE_SHORT";
                var transaction = new Transaction
                {
                    WalletId = wallet.WalletId,  // ← THIS WAS MISSING!
                    UserId = userId,
                    StockId = stock.StockId,
                    Quantity = quantity,
                    TransactionType = transactionType
                };
                _context.Transactions.Add(transaction);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Future contract created for {quantity} shares of {quote.CompanyName}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to create contract: " + ex.Message;
                Console.WriteLine(ex);
            }

            return RedirectToAction("Details", new { symbol });
        }


    }
}

