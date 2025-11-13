using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers
{
    public class DividendsController : Controller
    {
        private readonly EstocksDbContext _context;

        public DividendsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: /Dividends -> redirect users to their dividends (or to login)
        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Users");

            return RedirectToAction(nameof(MyDividends));
        }

        // GET: /Dividends/MyDividends
        // Shows only the dividends belonging to the currently logged-in user
        [Authorize]
        public async Task<IActionResult> MyDividends()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Users");
            }

            var dividends = await _context.Dividends
                .Where(d => d.UserId == userId)
                .Include(d => d.Stock)
                .OrderByDescending(d => d.ReceivedDate)
                .ToListAsync();

            return View(dividends);
        }


    }
}
