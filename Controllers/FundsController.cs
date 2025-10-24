using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Controllers
{
    [AllowAnonymous] // Everyone can access this controller
    public class FundsController : Controller
    {
        private readonly EstocksDbContext _context;

        public FundsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Funds - Shows list of all funds
        public async Task<IActionResult> Index()
        {
            var funds = await _context.Funds
                .OrderBy(f => f.FundName)
                .ToListAsync();

            return View(funds);
        }

        // GET: /Funds/Details/5 - Shows details of a specific fund
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var fund = await _context.Funds
                .FirstOrDefaultAsync(f => f.FundId == id);

            if (fund == null) return NotFound();

            return View(fund);
        }
    }
}