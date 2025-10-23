using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class FundsController : Controller
    {
        private readonly EstocksDbContext _context;

        public FundsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Funds
        public async Task<IActionResult> Index()
        {
            var list = await _context.Funds.ToListAsync();
            return Json(list);
        }

        // GET: /Funds/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null) return NotFound();
            return Json(fund);
        }

        // POST: /Funds/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Fund fund)
        {
            if (fund == null) return BadRequest();
            _context.Funds.Add(fund);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = fund.FundId }, fund);
        }

        // POST: /Funds/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] Fund fund)
        {
            if (fund == null || id != fund.FundId) return BadRequest();
            _context.Entry(fund).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: /Funds/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund == null) return NotFound();
            _context.Funds.Remove(fund);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
