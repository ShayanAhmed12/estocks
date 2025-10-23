using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class StocksController : Controller
    {
        private readonly EstocksDbContext _context;

        public StocksController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Stocks
        public async Task<IActionResult> Index()
        {
            var list = await _context.Stocks.ToListAsync();
            return Json(list);
        }

        // GET: /Stocks/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null) return NotFound();
            return Json(stock);
        }

        // POST: /Stocks/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Stock stock)
        {
            if (stock == null) return BadRequest();
            _context.Stocks.Add(stock);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = stock.StockId }, stock);
        }

        // POST: /Stocks/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] Stock stock)
        {
            if (stock == null || id != stock.StockId) return BadRequest();
            _context.Entry(stock).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: /Stocks/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null) return NotFound();
            _context.Stocks.Remove(stock);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
