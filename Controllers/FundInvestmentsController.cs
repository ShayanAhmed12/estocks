using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class FundInvestmentsController : Controller
    {
        private readonly EstocksDbContext _context;

        public FundInvestmentsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /FundInvestments
        public async Task<IActionResult> Index()
        {
            var list = await _context.FundInvestments.ToListAsync();
            return Json(list);
        }

        // GET: /FundInvestments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var investment = await _context.FundInvestments.FindAsync(id);
            if (investment == null) return NotFound();
            return Json(investment);
        }

        // POST: /FundInvestments/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FundInvestment investment)
        {
            if (investment == null) return BadRequest();
            _context.FundInvestments.Add(investment);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = investment.InvestmentId }, investment);
        }

        // POST: /FundInvestments/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] FundInvestment investment)
        {
            if (investment == null || id != investment.InvestmentId) return BadRequest();
            _context.Entry(investment).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: /FundInvestments/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var investment = await _context.FundInvestments.FindAsync(id);
            if (investment == null) return NotFound();
            _context.FundInvestments.Remove(investment);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Invest(int fundId, int amount)
        {
            // Get logged-in user
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                TempData["Error"] = "You must be logged in to invest.";
                return RedirectToAction("Details", "Funds", new { id = fundId });
            }

            // Get user's wallet
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null || wallet.Balance < amount)
            {
                TempData["Error"] = "Insufficient wallet balance to invest.";
                return RedirectToAction("Details", "Funds", new { id = fundId });
            }

            // Get the fund
            var fund = await _context.Funds.FirstOrDefaultAsync(f => f.FundId == fundId);
            if (fund == null)
            {
                TempData["Error"] = "Fund not found.";
                return RedirectToAction("Index", "Funds");
            }

            // Calculate units based on NAV
            decimal units = (decimal)amount / fund.NetAssetValue;

            // Create FundInvestment
            var investment = new FundInvestment
            {
                FundId = fundId,
                UserId = userId,
                Amount = amount,
                BuyPrice = fund.NetAssetValue,
                BuyDate = DateTime.Now,
                Maturity = DateTime.Now.AddYears(1) // example: 1-year maturity
            };

            _context.FundInvestments.Add(investment);

            // Deduct wallet balance
            wallet.Balance -= amount;
            wallet.LastUpdated = DateTime.Now;
            _context.Wallets.Update(wallet);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully invested PKR {amount:N0} in {fund.FundName} ({units:F2} units).";
            return RedirectToAction("Details", "Funds", new { id = fundId });
        }

    }
}
