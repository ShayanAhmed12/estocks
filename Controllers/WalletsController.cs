using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace WebApplication2.Controllers
{
    public class WalletsController : Controller
    {
        private readonly EstocksDbContext _context;

        public WalletsController(EstocksDbContext context)
        {
            _context = context;
        }

        // GET: Wallets/Index
        public async Task<IActionResult> Index()
        {
            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Users");
            }

            // Get the current user's ID from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Users");
            }

            // Get user with wallet and stocks
            var user = await _context.Users
                .Include(u => u.Wallets)
                .Include(u => u.Stocks)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound();
            }

            // Get or create wallet for user
            var wallet = user.Wallets?.FirstOrDefault();
            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = userId,
                    Balance = 0,
                    LastUpdated = DateTime.Now
                };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            // Pass the wallet to the view
            return View(wallet);
        }

        // POST: Wallets/AddFunds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFunds(int amount)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Users");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Users");
            }

            if (amount <= 0)
            {
                TempData["Error"] = "Please enter a valid amount greater than 0.";
                return RedirectToAction("Index");
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = userId,
                    Balance = amount,
                    LastUpdated = DateTime.Now
                };
                _context.Wallets.Add(wallet);
            }
            else
            {
                wallet.Balance += amount;
                wallet.LastUpdated = DateTime.Now;
                _context.Wallets.Update(wallet);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully added ${amount:N0} to your wallet!";
            return RedirectToAction("Index");
        }
    }
}