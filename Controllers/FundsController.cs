using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Security.Claims; // <--- needed
using System;

namespace WebApplication2.Controllers
{
    [AllowAnonymous] // Everyone can access list/details; investing requires login
    public class FundsController : Controller
    {
        private readonly EstocksDbContext _context;

        public FundsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Funds - Shows list of all funds (and user's investments if logged in)
        public async Task<IActionResult> Index()
        {
            var funds = await _context.Funds
                .OrderBy(f => f.FundName)
                .ToListAsync();

            // Load current user's fund investments (for display) if authenticated
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

        // GET: /Funds/Details/5 - Shows details of a specific fund
        // inside Controllers/FundsController.cs
        // add using at top if not already present:
        // using System.Security.Claims;

        // GET: /Funds/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var fund = await _context.Funds
                .FirstOrDefaultAsync(f => f.FundId == id);

            if (fund == null) return NotFound();

            // Prepare user's investments for this fund if authenticated
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


        // POST: /Funds/Invest/5
        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Invest(int id, int amount)
        {
            // id = fund id
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
                TempData["Error"] = $"Insufficient balance. Required: ${amount:N0}, Available: ${wallet.Balance:N0}";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                // Use decimal arithmetic to compute units
                decimal nav = fund.NetAssetValue; // NAV per unit (int -> decimal)
                decimal rawUnits = amount / nav;

                // Choose precision for units (e.g., 4 decimal places) and round down (so we never give more units than paid for)
                const int PRECISION = 4;
                decimal factor = (decimal)Math.Pow(10, PRECISION);
                decimal units = Math.Floor(rawUnits * factor) / factor; // round down truncation

                if (units <= 0)
                {
                    TempData["Error"] = $"Investment amount too small for NAV ${nav:N2}. Increase amount.";
                    return RedirectToAction("Details", new { id });
                }

                // actual amount used based on truncated units (decimal)
                decimal actualAmountUsedDecimal = units * nav;

                // convert to int for storage (floor to avoid overdrawing)
                int actualAmountUsedInt = (int)Math.Floor(actualAmountUsedDecimal);

                if (actualAmountUsedInt <= 0)
                {
                    TempData["Error"] = $"After rounding the amount to units, purchase amount is too small. Increase investment.";
                    return RedirectToAction("Details", new { id });
                }

                // Double-check wallet sufficiency (in case amount was borderline)
                if (wallet.Balance < actualAmountUsedInt)
                {
                    TempData["Error"] = $"Insufficient balance after rounding. Required: ${actualAmountUsedInt:N0}, Available: ${wallet.Balance:N0}";
                    return RedirectToAction("Details", new { id });
                }

                // Deduct only the actual amount used; leftover (amount - actualAmountUsedInt) remains in wallet
                wallet.Balance -= actualAmountUsedInt;
                wallet.LastUpdated = DateTime.Now;
                _context.Wallets.Update(wallet);

                // Create FundInvestment record (we keep same model: Amount (int) and BuyPrice (int))
                var investment = new FundInvestment
                {
                    FundId = fund.FundId,
                    UserId = userId,
                    Amount = actualAmountUsedInt,     // stored as int
                    BuyPrice = fund.NetAssetValue,    // store NAV at purchase time
                    BuyDate = DateTime.Now,
                    Maturity = DateTime.Now.AddYears(1) // default maturity; change if fund-specific
                };
                _context.FundInvestments.Add(investment);

                // Create transaction for record-keeping
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

                // Build friendly message showing units (formatted with the chosen precision)
                string unitsText = units.ToString($"F{PRECISION}");
                TempData["Success"] = $"Invested ${actualAmountUsedInt:N0} into {fund.FundName} at NAV ${nav:N2}. Units purchased: {unitsText}";

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
