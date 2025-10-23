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
    }
}
