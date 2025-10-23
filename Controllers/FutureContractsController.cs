using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class FutureContractsController : Controller
    {
        private readonly EstocksDbContext _context;

        public FutureContractsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /FutureContracts
        public async Task<IActionResult> Index()
        {
            var list = await _context.FutureContracts.ToListAsync();
            return Json(list);
        }

        // GET: /FutureContracts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var contract = await _context.FutureContracts.FindAsync(id);
            if (contract == null) return NotFound();
            return Json(contract);
        }

        // POST: /FutureContracts/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FutureContract contract)
        {
            if (contract == null) return BadRequest();
            _context.FutureContracts.Add(contract);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = contract.ContractId }, contract);
        }

        // POST: /FutureContracts/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] FutureContract contract)
        {
            if (contract == null || id != contract.ContractId) return BadRequest();
            _context.Entry(contract).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: /FutureContracts/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var contract = await _context.FutureContracts.FindAsync(id);
            if (contract == null) return NotFound();
            _context.FutureContracts.Remove(contract);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
