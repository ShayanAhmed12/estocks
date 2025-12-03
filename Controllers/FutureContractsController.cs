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


        public async Task<IActionResult> Index()
        {
            
            await AutoCloseExpiredContracts();

            var contracts = await _context.FutureContracts
                .Include(fc => fc.Stock)
                .Include(fc => fc.FutureTradings)
                    .ThenInclude(ft => ft.User)
                .ToListAsync();

            return View(contracts);
        }


        public async Task<IActionResult> Details(int id)
        {
            var contract = await _context.FutureContracts
                .Include(fc => fc.Stock)
                .Include(fc => fc.FutureTradings)
                .FirstOrDefaultAsync(fc => fc.ContractId == id);

            if (contract == null) return NotFound();

            return View(contract);
        }


        [HttpPost]
        public async Task<IActionResult> CreateLong([FromBody] CreateContractRequest request)
        {
            return await CreateContract(request, "LONG");
        }


        [HttpPost]
        public async Task<IActionResult> CreateShort([FromBody] CreateContractRequest request)
        {
            return await CreateContract(request, "SHORT");
        }


        private async Task<IActionResult> CreateContract(CreateContractRequest request, string positionType)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = "Contract data is required" });

             
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

  
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null)
                {
                    return Json(new { success = false, message = "Wallet not found" });
                }

 
                int totalCost = request.Quantity * request.ContractPrice;
                int notional = request.Quantity * request.ContractPrice;
                int margin = (int)Math.Ceiling(notional * 0.15);


                if (wallet.Balance < margin)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Insufficient balance. Required margin: {margin} PKR"
                    });
                }

                wallet.Balance -= margin;
                wallet.LastUpdated = DateTime.Now;


                var stock = await _context.Stocks.FindAsync(request.StockId);
                if (stock == null)
                {
                    return Json(new { success = false, message = "Stock not found" });
                }

         
                stock.Price = request.ContractPrice;

         
                var contract = await _context.FutureContracts
                    .FirstOrDefaultAsync(fc => fc.StockId == request.StockId &&
                                              fc.ExpiryDate == request.ExpiryDate &&
                                              fc.ContractType == positionType);

                if (contract == null)
                {
                    contract = new FutureContract
                    {
                        StockId = request.StockId,
                        ExpiryDate = request.ExpiryDate,
                        ContractPrice = request.ContractPrice,
                        ContractType = positionType
                    };
                    _context.FutureContracts.Add(contract);
                    await _context.SaveChangesAsync(); 
                }

        
                var futureTrading = new FutureTrading
                {
                    UserId = userId,
                    ContractId = contract.ContractId,
                    Quantity = request.Quantity,
                    Price = request.ContractPrice,
                    TradeTime = DateTime.Now
                };
                _context.FutureTradings.Add(futureTrading);

    
                string orderType = positionType == "LONG" ? "future_long" : "future_short";
                var order = new Order
                {
                    UserId = userId,
                    StockId = request.StockId,
                    OrderType = orderType,
                    Quantity = request.Quantity,
                    Price = request.ContractPrice,
                    OrderStatus = true
                };
                _context.Orders.Add(order);

      
                string transactionType = positionType == "LONG" ? "BUY_FUTURE_LONG" : "SELL_FUTURE_SHORT";
                var transaction = new Transaction
                {
                    WalletId = wallet.WalletId,
                    UserId = userId,
                    StockId = request.StockId,
                    Quantity = request.Quantity,
                    TransactionType = transactionType
                };
                _context.Transactions.Add(transaction);

      
                Console.WriteLine($"Adding Order: UserId={userId}, StockId={request.StockId}, Type={orderType}");
                Console.WriteLine($"Adding Transaction: WalletId={wallet.WalletId}, UserId={userId}, StockId={request.StockId}, Type={transactionType}");


                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{positionType} position created successfully",
                    contractId = contract.ContractId,
                    positionType = positionType,
                    amountDeducted = totalCost,
                    expiryDate = contract.ExpiryDate.ToString("MMM dd, yyyy"),
                    remainingBalance = wallet.Balance
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateContract: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

     
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

  
        private async Task<IActionResult> ClosePosition(FutureTrading futureTrading, int userId)
        {
            try
            {
                
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null)
                {
                    return Json(new { success = false, message = "Wallet not found" });
                }

   
                var stock = await _context.Stocks.FindAsync(futureTrading.Contract.StockId);
                int currentPrice = stock?.Price ?? futureTrading.Price;

      
                int profitLoss;
                if (futureTrading.Contract.ContractType == "LONG")
                {
                    profitLoss = (currentPrice - futureTrading.Price) * futureTrading.Quantity;
                }
                else 
                {
                    profitLoss = (futureTrading.Price - currentPrice) * futureTrading.Quantity;
                }

           
                int notional = futureTrading.Price * futureTrading.Quantity;
                int margin = (int)Math.Ceiling(notional * 0.15);
                int settlementAmount = margin + profitLoss;

        
                wallet.Balance += settlementAmount;
                wallet.LastUpdated = DateTime.Now;


        
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.UserId == userId &&
                                            o.StockId == futureTrading.Contract.StockId &&
                                            (o.OrderType == "future_long" || o.OrderType == "future_short") &&
                                            o.OrderStatus == true);

                if (order != null)
                {
                    order.OrderStatus = false;
                }

      
                string transactionType = futureTrading.Contract.ContractType == "LONG"
                    ? "CLOSE_FUTURE_LONG"
                    : "CLOSE_FUTURE_SHORT";

                var closeTransaction = new Transaction
                {
                    WalletId = wallet.WalletId,
                    UserId = userId,
                    StockId = futureTrading.Contract.StockId,
                    Quantity = futureTrading.Quantity,
                    TransactionType = transactionType
                };
                _context.Transactions.Add(closeTransaction);

      
                _context.FutureTradings.Remove(futureTrading);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{futureTrading.Contract.ContractType} position closed successfully",
                    positionType = futureTrading.Contract.ContractType,
                    entryPrice = futureTrading.Price,
                    exitPrice = currentPrice,
                    margin = margin,
                    profitLoss = profitLoss,
                    profitLossPercent = notional > 0 ? ((double)profitLoss / notional * 100).ToString("F2") : "0",
                    settlementAmount = settlementAmount,
                    newBalance = wallet.Balance
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ClosePosition: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }


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


    public class CreateContractRequest
    {
        public int StockId { get; set; }
        public int Quantity { get; set; }
        public int ContractPrice { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}