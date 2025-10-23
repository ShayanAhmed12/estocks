using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class DividendController : Controller
    {
        private readonly EstocksDbContext _context;

        public DividendController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Dividend or /Dividend/Index
        [HttpGet]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Dividends.ToListAsync();
            return View(list);
        }

        // GET: /Dividend/Details/5
        [HttpGet("Details/{id?}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Dividends.FirstOrDefaultAsync(d => d.DividendId == id.Value);
            if (item == null) return NotFound();
            return View(item);
        }

        // GET: /Dividend/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Dividend/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StockId,UserId,Amount,ReceivedDate")] Dividend item)
        {
            if (ModelState.IsValid)
            {
                _context.Dividends.Add(item);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        // GET: /Dividend/Edit/5
        [HttpGet("Edit/{id?}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Dividends.FindAsync(id.Value);
            if (item == null) return NotFound();
            return View(item);
        }

        // POST: /Dividend/Edit/5
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DividendId,StockId,UserId,Amount,ReceivedDate")] Dividend item)
        {
            if (id != item.DividendId) return BadRequest();
            if (!ModelState.IsValid) return View(item);

            try
            {
                _context.Update(item);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await DividendExists(item.DividendId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Dividend/Delete/5
        [HttpGet("Delete/{id?}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Dividends.FirstOrDefaultAsync(d => d.DividendId == id.Value);
            if (item == null) return NotFound();
            return View(item);
        }

        // POST: /Dividend/Delete/5
        [HttpPost("Delete/{id}")]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Dividends.FindAsync(id);
            if (item != null)
            {
                _context.Dividends.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> DividendExists(int id)
        {
            return await _context.Dividends.AnyAsync(e => e.DividendId == id);
        }
    }
}

