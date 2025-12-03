using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Security.Claims; 
using System;

namespace WebApplication2.Controllers
{
    [AllowAnonymous] 
    public class FundsController : Controller
    {
        private readonly EstocksDbContext _context;

        public FundsController(EstocksDbContext context)
        {
            _context = context;
        }

       
        public async Task<IActionResult> Index()
        {
            var funds = await _context.Funds
                .OrderBy(f => f.FundName)
                .ToListAsync();

            if (User.Identity?.IsAuthenticated ?? false)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    var userInvestments = await _context.FundInvestments
                        .Include(fi => fi.Fund)
                        .Where(fi => fi.UserId == userId)
                        .OrderByDescending(fi => fi.BuyDate)
                        .ToListAsync();

                    ViewBag.UserInvestments = userInvestments;
                }
            }

            return View(funds);
        }

   
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var fund = await _context.Funds
                .FirstOrDefaultAsync(f => f.FundId == id);

            if (fund == null) return NotFound();

       
            var investmentsForFund = new List<WebApplication2.Models.FundInvestment>();

            if (User.Identity?.IsAuthenticated ?? false)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                {
                    investmentsForFund = await _context.FundInvestments
                        .Where(fi => fi.FundId == fund.FundId && fi.UserId == userId)
                        .OrderByDescending(fi => fi.BuyDate)
                        .ToListAsync();
                }
            }

            ViewBag.UserInvestmentsForFund = investmentsForFund;

            return View(fund);
        }


   
        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Invest(int id, int amount)
        {
     
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                TempData["Error"] = "Please login to invest.";
                return RedirectToAction("Details", new { id });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                TempData["Error"] = "Unable to determine user identity. Please login again.";
                return RedirectToAction("Details", new { id });
            }

            if (amount <= 0)
            {
                TempData["Error"] = "Please enter a positive investment amount.";
                return RedirectToAction("Details", new { id });
            }

            var fund = await _context.Funds.FirstOrDefaultAsync(f => f.FundId == id);
            if (fund == null)
            {
                TempData["Error"] = "Fund not found.";
                return RedirectToAction("Index");
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                TempData["Error"] = "Wallet not found. Please setup your wallet before investing.";
                return RedirectToAction("Details", new { id });
            }

            if (wallet.Balance < amount)
            {
                TempData["Error"] = $"Insufficient balance. Required: PKR {amount:N0}, Available: PKR {wallet.Balance:N0}";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                
                decimal nav = fund.NetAssetValue; 
                decimal rawUnits = amount / nav;

               
                const int PRECISION = 4;
                decimal factor = (decimal)Math.Pow(10, PRECISION);
                decimal units = Math.Floor(rawUnits * factor) / factor; 

                if (units <= 0)
                {
                    TempData["Error"] = $"Investment amount too small for NAV PKR {nav:N2}. Increase amount.";
                    return RedirectToAction("Details", new { id });
                }

              
                decimal actualAmountUsedDecimal = units * nav;

              
                int actualAmountUsedInt = (int)Math.Floor(actualAmountUsedDecimal);

                if (actualAmountUsedInt <= 0)
                {
                    TempData["Error"] = $"After rounding the amount to units, purchase amount is too small. Increase investment.";
                    return RedirectToAction("Details", new { id });
                }

             
                if (wallet.Balance < actualAmountUsedInt)
                {
                    TempData["Error"] = $"Insufficient balance after rounding. Required: PKR {actualAmountUsedInt:N0}, Available: PKR {wallet.Balance:N0}";
                    return RedirectToAction("Details", new { id });
                }

   
                wallet.Balance -= actualAmountUsedInt;
                wallet.LastUpdated = DateTime.Now;
                _context.Wallets.Update(wallet);

             
                var investment = new FundInvestment
                {
                    FundId = fund.FundId,
                    UserId = userId,
                    Amount = actualAmountUsedInt,     
                    BuyPrice = fund.NetAssetValue,    
                    BuyDate = DateTime.Now,
                    Maturity = DateTime.Now.AddYears(1) 
                };
                _context.FundInvestments.Add(investment);

      
                var tx = new Transaction
                {
                    WalletId = wallet.WalletId,
                    UserId = userId,
                    StockId = null,
                    Quantity = 0,
                    TransactionType = "FUND_INVEST"
                };
                _context.Transactions.Add(tx);

                await _context.SaveChangesAsync();

               
                string unitsText = units.ToString($"F{PRECISION}");
                TempData["Success"] = $"Invested PKR {actualAmountUsedInt:N0} into {fund.FundName} at NAV PKR {nav:N2}. Units purchased: {unitsText}";

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                TempData["Error"] = $"Failed to invest: {ex.Message}";
            }

            return RedirectToAction("Details", new { id });
        }


    }
}
