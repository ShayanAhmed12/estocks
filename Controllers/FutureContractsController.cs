using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Security.Claims;

namespace WebApplication2.Controllers
{
    public class FutureContractsController : Controller
    {
        private readonly EstocksDbContext _context;

        public FutureContractsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /FutureContracts
        public async Task<IActionResult> Index()
        {
            // Check for expired contracts and auto-close them
            await AutoCloseExpiredContracts();

            var contracts = await _context.FutureContracts
                .Include(fc => fc.Stock)
                .Include(fc => fc.FutureTradings)
                    .ThenInclude(ft => ft.User)
                .ToListAsync();

            return View(contracts);
        }

        // GET: /FutureContracts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var contract = await _context.FutureContracts
                .Include(fc => fc.Stock)
                .Include(fc => fc.FutureTradings)
                .FirstOrDefaultAsync(fc => fc.ContractId == id);

            if (contract == null) return NotFound();

            return View(contract);
        }

        // POST: /FutureContracts/CreateLong (Buy Contract)
        [HttpPost]
        public async Task<IActionResult> CreateLong([FromBody] CreateContractRequest request)
        {
            return await CreateContract(request, "LONG");
        }

        // POST: /FutureContracts/CreateShort (Sell Contract)
        [HttpPost]
        public async Task<IActionResult> CreateShort([FromBody] CreateContractRequest request)
        {
            return await CreateContract(request, "SHORT");
        }

        // Private method to handle both LONG and SHORT
        private async Task<IActionResult> CreateContract(CreateContractRequest request, string positionType)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = "Contract data is required" });

                // Get authenticated user ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Get user's wallet
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null)
                {
                    return Json(new { success = false, message = "Wallet not found" });
                }

                // Calculate margin requirement (typically 10-20% of total value)
                int contractValue = request.Quantity * request.ContractPrice;
                int marginRequired = (int)(contractValue * 0.15); // 15% margin

                // Check if user has enough balance
                if (wallet.Balance < marginRequired)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Insufficient balance. Margin required: ${marginRequired}, Available: ${wallet.Balance}"
                    });
                }

                // Deduct margin from wallet
                wallet.Balance -= marginRequired;
                wallet.LastUpdated = DateTime.Now;

                // Create or get the future contract
                var contract = await _context.FutureContracts
                    .FirstOrDefaultAsync(fc => fc.StockId == request.StockId &&
                                              fc.ExpiryDate == request.ExpiryDate);

                if (contract == null)
                {
                    contract = new FutureContract
                    {
                        StockId = request.StockId,
                        ExpiryDate = request.ExpiryDate,
                        ContractPrice = request.ContractPrice,
                        ContractType = positionType // LONG or SHORT
                    };
                    _context.FutureContracts.Add(contract);
                    await _context.SaveChangesAsync(); // Save to get ContractId
                }

                // Create FutureTrading entry (user's position)
                var futureTrading = new FutureTrading
                {
                    UserId = userId,
                    ContractId = contract.ContractId,
                    Quantity = request.Quantity,
                    Price = request.ContractPrice, // Entry price
                    TradeTime = DateTime.Now
                };
                _context.FutureTradings.Add(futureTrading);

                // Create Order entry
                string orderType = positionType == "LONG" ? "future_long" : "future_short";
                var order = new Order
                {
                    UserId = userId,
                    StockId = request.StockId,
                    Quantity = request.Quantity,
                    Price = request.ContractPrice,
                    OrderType = orderType,
                    OrderStatus = true // Open position
                };
                _context.Orders.Add(order);

                // Create Transaction entry
                string transactionType = positionType == "LONG" ? "BUY_FUTURE_LONG" : "SELL_FUTURE_SHORT";
                var transaction = new Transaction
                {
                    UserId = userId,
                    StockId = request.StockId,
                    Quantity = request.Quantity,
                    TransactionType = transactionType
                };
                _context.Transactions.Add(transaction);

                // Save all changes
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{positionType} position created successfully",
                    contractId = contract.ContractId,
                    positionType = positionType,
                    marginDeducted = marginRequired,
                    contractValue = contractValue,
                    expiryDate = contract.ExpiryDate.ToString("MMM dd, yyyy"),
                    remainingBalance = wallet.Balance
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: /FutureContracts/Close/{id}
        [HttpPost]
        public async Task<IActionResult> Close(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false, message = "Not authenticated" });
                }

                // Find the future trading record
                var futureTrading = await _context.FutureTradings
                    .Include(ft => ft.Contract)
                        .ThenInclude(c => c.Stock)
                    .FirstOrDefaultAsync(ft => ft.FutureTradingId == id && ft.UserId == userId);

                if (futureTrading == null)
                {
                    return Json(new { success = false, message = "Position not found" });
                }

                return await ClosePosition(futureTrading, userId);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Private method to close a position
        private async Task<IActionResult> ClosePosition(FutureTrading futureTrading, int userId)
        {
            // Get user's wallet
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                return Json(new { success = false, message = "Wallet not found" });
            }

            // Get current stock price
            var stock = await _context.Stocks.FindAsync(futureTrading.Contract.StockId);
            int currentPrice = stock?.Price ?? futureTrading.Price;

            // Calculate profit/loss based on position type
            int profitLoss;
            if (futureTrading.Contract.ContractType == "LONG")
            {
                // LONG: profit when price goes up
                profitLoss = (currentPrice - futureTrading.Price) * futureTrading.Quantity;
            }
            else // SHORT
            {
                // SHORT: profit when price goes down
                profitLoss = (futureTrading.Price - currentPrice) * futureTrading.Quantity;
            }

            // Calculate margin that was held
            int marginHeld = (int)(futureTrading.Price * futureTrading.Quantity * 0.15);

            // Return margin + profit/loss to wallet
            int totalReturn = marginHeld + profitLoss;
            wallet.Balance += totalReturn;
            wallet.LastUpdated = DateTime.Now;

            // Close the related order
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.UserId == userId &&
                                        o.StockId == futureTrading.Contract.StockId &&
                                        (o.OrderType == "future_long" || o.OrderType == "future_short") &&
                                        o.OrderStatus == true);

            if (order != null)
            {
                order.OrderStatus = false;
            }

            // Create closing transaction
            string transactionType = futureTrading.Contract.ContractType == "LONG"
                ? "CLOSE_FUTURE_LONG"
                : "CLOSE_FUTURE_SHORT";

            var closeTransaction = new Transaction
            {
                UserId = userId,
                StockId = futureTrading.Contract.StockId,
                Quantity = futureTrading.Quantity,
                TransactionType = transactionType
            };
            _context.Transactions.Add(closeTransaction);

            // Remove future trading record
            _context.FutureTradings.Remove(futureTrading);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"{futureTrading.Contract.ContractType} position closed successfully",
                positionType = futureTrading.Contract.ContractType,
                entryPrice = futureTrading.Price,
                exitPrice = currentPrice,
                profitLoss = profitLoss,
                profitLossPercent = ((double)profitLoss / marginHeld * 100).ToString("F2"),
                marginReturned = marginHeld,
                totalReturn = totalReturn,
                newBalance = wallet.Balance
            });
        }

        // Background job to auto-close expired contracts
        private async Task AutoCloseExpiredContracts()
        {
            var expiredTradings = await _context.FutureTradings
                .Include(ft => ft.Contract)
                    .ThenInclude(c => c.Stock)
                .Where(ft => ft.Contract.ExpiryDate <= DateTime.Now)
                .ToListAsync();

            foreach (var trading in expiredTradings)
            {
                await ClosePosition(trading, trading.UserId.Value);
            }

            if (expiredTradings.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        // POST: /FutureContracts/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var contract = await _context.FutureContracts.FindAsync(id);
            if (contract == null) return NotFound();

            _context.FutureContracts.Remove(contract);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Deleted successfully" });
        }
    }

    // Helper class for Create request
    public class CreateContractRequest
    {
        public int StockId { get; set; }
        public int Quantity { get; set; }
        public int ContractPrice { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}
