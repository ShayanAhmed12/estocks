using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using WebApplication2.Data;
using WebApplication2.Models;
using System.Threading.Tasks;

namespace WebApplication2.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly EstocksDbContext _context;

        public DashboardController(EstocksDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            var user = await _context.Users
                .Include(u => u.Wallets)
                .Include(u => u.Orders)
                .Include(u => u.FundInvestments)
                .Include(u => u.Dividends)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return NotFound();

            ViewBag.OrdersCount = user.Orders?.Count ?? 0;
            ViewBag.FundInvestmentsCount = user.FundInvestments?.Count ?? 0;
            ViewBag.DividendsCount = user.Dividends?.Count ?? 0;
            ViewBag.HasWallet = (user.Wallets != null && user.Wallets.Any());
            ViewBag.WalletBalance = user.Wallets?.FirstOrDefault()?.Balance ?? 0;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                TempData["Error"] = "Please provide the new password and confirmation.";
                return RedirectToAction(nameof(Index));
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound();

         
            if (user.Password != currentPassword)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction(nameof(Index));
            }

            user.Password = newPassword;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction(nameof(Index));
        }

    
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmail(string newEmail, string confirmPassword)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            if (string.IsNullOrEmpty(newEmail))
            {
                TempData["Error"] = "Please provide a new email address.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound();

            if (user.Password != confirmPassword)
            {
                TempData["Error"] = "Confirmation password is incorrect.";
                return RedirectToAction(nameof(Index));
            }

        
            var exists = await _context.Users.AnyAsync(u => u.Email == newEmail && u.UserId != userId);
            if (exists)
            {
                TempData["Error"] = "The provided email is already used by another account.";
                return RedirectToAction(nameof(Index));
            }

            user.Email = newEmail;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Email updated successfully.";
            return RedirectToAction(nameof(Index));
        }

   
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string confirmPassword)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound();

            if (user.Password != confirmPassword)
            {
                TempData["Error"] = "Confirmation password is incorrect. Account not deactivated.";
                return RedirectToAction(nameof(Index));
            }


            user.ActiveUser = false;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

          
            await HttpContext.SignOutAsync();

            TempData["Success"] = "Your account has been deactivated. We're sorry to see you go.";
            return RedirectToAction("Login", "Users");
        }

      
        public async Task<IActionResult> Billing()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            var user = await _context.Users
                .Include(u => u.Banks) 
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return NotFound();

            return View(user);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBilling(string BankName, string AccountTitle, string AccountType, string AccountNumber)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            if (string.IsNullOrEmpty(BankName) || string.IsNullOrEmpty(AccountTitle) || string.IsNullOrEmpty(AccountType) || string.IsNullOrEmpty(AccountNumber))
            {
                TempData["Error"] = "Please provide all bank details.";
                return RedirectToAction("Billing");
            }

            var model = new Bank
            {
                UserId = userId,
                BankName = BankName,
                AccountTitle = AccountTitle,
                AccountType = AccountType,
                AccountNumber = AccountNumber
            };

            _context.Banks.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Bank details saved successfully.";
            return RedirectToAction("Billing");
        }




   
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBank(int bankId, string confirmPassword)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return RedirectToAction("Login", "Users");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound();

            if (user.Password != confirmPassword)
            {
                TempData["Error"] = "Password is incorrect. Bank record was not deleted.";
                return RedirectToAction("Billing");
            }

            var bank = await _context.Banks.FirstOrDefaultAsync(b => b.BankId == bankId && b.UserId == userId);
            if (bank == null)
            {
                TempData["Error"] = "Bank record not found.";
                return RedirectToAction("Billing");
            }

            _context.Banks.Remove(bank);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Bank record deleted successfully.";
            return RedirectToAction("Billing");
        }





    }
}
