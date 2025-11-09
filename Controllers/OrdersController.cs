using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    [Authorize]
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
            // get current user id from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Users");

            if (!int.TryParse(userIdClaim.Value, out var userId)) return RedirectToAction("Login", "Users");

            // load transactions for the user with stock navigation
            var transactions = await _context.Transactions
                .Include(t => t.Stock)
                .Where(t => t.UserId == userId && t.StockId != null)
                .ToListAsync();

            // Aggregate transactions by stock and compute net quantity
            var holdings = transactions
                .GroupBy(t => t.StockId)
                .Where(g => g.Key != null)
                .Select(g => new
                {
                    StockId = g.Key!.Value,
                    Stock = g.First().Stock,
                    NetQty = g.Sum(t => t.TransactionType.ToLower() == "buy" ? t.Quantity : -t.Quantity)
                })
                .Where(h => h.NetQty > 0)
                .ToList();

            // For each holding create an Order record of type 'holding' if one does not already exist
            var added = false;
            foreach (var h in holdings)
            {
                var exists = await _context.Orders.AnyAsync(o => o.UserId == userId && o.StockId == h.StockId && o.OrderType == "holding");
                if (!exists)
                {
                    var order = new Order
                    {
                        UserId = userId,
                        StockId = h.StockId,
                        OrderType = "holding",
                        Quantity = h.NetQty,
                        Price = h.Stock?.Price ?? 0,
                        OrderStatus = true
                    };

                    _context.Orders.Add(order);
                    added = true;
                }
            }

            if (added)
            {
                await _context.SaveChangesAsync();
            }

            // Load orders for the user (show these in the view)
            var orders = await _context.Orders
                .Include(o => o.Stock)
                .Where(o => o.UserId == userId)
                .ToListAsync();

            return View(orders);
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
