using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class WalletsController : Controller
    {
        private readonly EstocksDbContext _context;

        public WalletsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Wallets
        public async Task<IActionResult> Index()
        {
            var list = await _context.Wallets.ToListAsync();
            return Json(list);
        }

        // GET: /Wallets/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var wallet = await _context.Wallets.FindAsync(id);
            if (wallet == null) return NotFound();
            return Json(wallet);
        }

        // POST: /Wallets/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Wallet wallet)
        {
            if (wallet == null) return BadRequest();
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = wallet.WalletId }, wallet);
        }

        // POST: /Wallets/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] Wallet wallet)
        {
            if (wallet == null || id != wallet.WalletId) return BadRequest();
            _context.Entry(wallet).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: /Wallets/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var wallet = await _context.Wallets.FindAsync(id);
            if (wallet == null) return NotFound();
            _context.Wallets.Remove(wallet);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
