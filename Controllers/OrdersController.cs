using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class OrdersController : Controller
    {
        private readonly EstocksDbContext _context;

        public OrdersController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Orders
        public async Task<IActionResult> Index()
        {
            var list = await _context.Orders.ToListAsync();
            return Json(list);
        }

        // GET: /Orders/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();
            return Json(order);
        }

        // POST: /Orders/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Order order)
        {
            if (order == null) return BadRequest();
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(Details), new { id = order.OrderId }, order);
        }

        // POST: /Orders/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromBody] Order order)
        {
            if (order == null || id != order.OrderId) return BadRequest();
            _context.Entry(order).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: /Orders/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
