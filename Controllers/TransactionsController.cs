using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class TransactionsController : Controller
    {
        private readonly EstocksDbContext _context;

        public TransactionsController(EstocksDbContext context)
        {
            _context = context;
        }


        public async Task<IActionResult> Index()
        {
            var list = await _context.Transactions.ToListAsync();
            return Json(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null) return NotFound();
            return Json(transaction);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Transaction transaction)
        {
            if (transaction == null) return BadRequest();
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = transaction.TransactionId }, transaction);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] Transaction transaction)
        {
            if (transaction == null || id != transaction.TransactionId) return BadRequest();
            _context.Entry(transaction).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null) return NotFound();
            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
