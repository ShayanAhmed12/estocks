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

   
        public async Task<IActionResult> Index()
        {
            try
            {
                
                var availableStocks = _stockDataService.GetAvailableStocks();
                var symbols = availableStocks.Keys.ToList();

             
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

     
        public async Task<IActionResult> Details(string symbol, string period = "1mo")
        {
            if (string.IsNullOrEmpty(symbol))
                return NotFound();


            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
                return NotFound();

            quote.Change = quote.CurrentPrice - quote.PreviousClose;
            quote.ChangePercent = quote.PreviousClose != 0
                ? (quote.Change / quote.PreviousClose) * 100
                : 0;

        
            var historicalData = await _stockDataService.GetHistoricalData(symbol, period);

    
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
            ViewBag.SelectedPeriod = period; 

 
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

 
        public async Task<IActionResult> Portfolio()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Users");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            var portfolio = await _context.Transactions
                .Where(t => t.UserId == userId)
                .GroupBy(t => new { t.StockId, t.Stock.CompanyName })
                .Select(g => new
                {
                    StockId = g.Key.StockId,
                    CompanyName = g.Key.CompanyName,
                    Quantity = g.Sum(t => t.TransactionType == "BUY_SPOT" ? t.Quantity : -t.Quantity)
                })
                .Where(p => p.Quantity > 0) 
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


            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
            {
                TempData["Error"] = "Unable to fetch stock price. Please try again.";
                return RedirectToAction("Details", new { symbol });
            }

            int totalCost = (int)(quote.CurrentPrice * quantity);

     
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null || wallet.Balance < totalCost)
            {
                TempData["Error"] = $"Insufficient balance. Required: ${totalCost:N0}, Available: ${wallet?.Balance ?? 0:N0}";
                return RedirectToAction("Details", new { symbol });
            }


            wallet.Balance -= totalCost;
            wallet.LastUpdated = DateTime.Now;

     
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.CompanyName == quote.CompanyName && s.UserId == userId);

            if (stock != null)
            {
          
                stock.Price = (int)quote.CurrentPrice;
            }
            else
            {
      
                stock = new Stock
                {
                    CompanyName = quote.CompanyName,
                    Price = (int)quote.CurrentPrice,
                    UserId = userId
                };
                _context.Stocks.Add(stock);
                await _context.SaveChangesAsync(); 
            }

            try
            {
                
                var spotTrade = new SpotTrading
                {
                    UserId = userId,
                    StockId = stock.StockId,
                    Quantity = quantity,
                    Price = (int)quote.CurrentPrice,
                    TradeTime = DateTime.Now
                };
                _context.SpotTradings.Add(spotTrade);

             
                var transaction = new Transaction
                {
                    WalletId = wallet.WalletId,
                    UserId = userId,
                    StockId = stock.StockId,
                    Quantity = quantity,
                    TransactionType = "BUY_SPOT"
                };
                _context.Transactions.Add(transaction);

              
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

       
                var entries = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                    .Select(e => new { e.Entity.GetType().Name, e.State })
                    .ToList();

          
                TempData["SavePreview"] = System.Text.Json.JsonSerializer.Serialize(entries);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully purchased {quantity} shares of {quote.CompanyName} for ${totalCost:N0}";
            }
            catch (Exception ex)
            {
                
                TempData["Error"] = "Failed to complete purchase: " + ex.Message;

          
                Console.WriteLine(ex); 
            }

            return RedirectToAction("Details", new { symbol });
        }



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

            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
            {
                TempData["Error"] = "Unable to fetch stock price. Please try again.";
                return RedirectToAction("Details", new { symbol });
            }

         
            var ownedStock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.CompanyName == quote.CompanyName && s.UserId == userId);

            if (ownedStock == null)
            {
                TempData["Error"] = "You don't own this stock.";
                return RedirectToAction("Details", new { symbol });
            }

       
            var ownedQuantity = await _context.Transactions
                .Where(t => t.UserId == userId && t.StockId == ownedStock.StockId)
                .SumAsync(t => t.TransactionType == "BUY_SPOT" ? t.Quantity : -t.Quantity);

         
            if (ownedQuantity < quantity)
            {
                TempData["Error"] = $"Insufficient shares. You own {ownedQuantity} shares but tried to sell {quantity}.";
                return RedirectToAction("Details", new { symbol });
            }

            int totalRevenue = (int)(quote.CurrentPrice * quantity);

      
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                TempData["Error"] = "Wallet not found.";
                return RedirectToAction("Details", new { symbol });
            }

            try
            {
        
                wallet.Balance += totalRevenue;
                wallet.LastUpdated = DateTime.Now;

                var spotTrade = new SpotTrading
                {
                    UserId = userId,
                    StockId = ownedStock.StockId,
                    Quantity = -quantity, 
                    Price = (int)quote.CurrentPrice,
                    TradeTime = DateTime.Now
                };
                _context.SpotTradings.Add(spotTrade);

        
                var transaction = new Transaction
                {
                    WalletId = wallet.WalletId,
                    UserId = userId,
                    StockId = ownedStock.StockId,
                    Quantity = quantity,
                    TransactionType = "SELL_SPOT"
                };
                _context.Transactions.Add(transaction);

               
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


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyFuture(string symbol, int quantity, DateTime expiryDate, string contractType)
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Users");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

    
            var quote = await _stockDataService.GetStockQuote(symbol);
            if (quote == null)
            {
                TempData["Error"] = "Unable to fetch stock price.";
                return RedirectToAction("Details", new { symbol });
            }

            int contractPrice = (int)quote.CurrentPrice;
            int notional = contractPrice * quantity;

            int margin = (int)Math.Ceiling(notional * 0.15);

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null || wallet.Balance < margin)
            {
                TempData["Error"] = $"Insufficient balance. Required margin: {margin} PKR, Available: {wallet?.Balance ?? 0} PKR";
                return RedirectToAction("Details", new { symbol });
            }

       
            wallet.Balance -= margin;
            wallet.LastUpdated = DateTime.Now;

   
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
          
                var futureContract = new FutureContract
                {
                    StockId = stock.StockId,
                    ExpiryDate = expiryDate,
                    ContractPrice = contractPrice,
                    ContractType = contractType
                };
                _context.FutureContracts.Add(futureContract);
                await _context.SaveChangesAsync();

          
                var futureTrade = new FutureTrading
                {
                    ContractId = futureContract.ContractId,
                    UserId = userId,
                    Quantity = quantity,
                    Price = contractPrice,
                    TradeTime = DateTime.Now
                };
                _context.FutureTradings.Add(futureTrade);

        
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

               
                var transaction = new Transaction
                {
                    WalletId = wallet.WalletId,
                    UserId = userId,
                    StockId = stock.StockId,
                    Quantity = quantity,
                    TransactionType = "MARGIN_RESERVE"
                };
                _context.Transactions.Add(transaction);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Future position opened using {margin} PKR margin.";
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

