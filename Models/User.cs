using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        // Required fields — initialized with empty string
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public long Cnic { get; set; }

        [Required]
        public string PhoneNum { get; set; } = string.Empty;

        public bool ActiveUser { get; set; } = true;

        // Navigation properties — nullable because collections may not always be set
        public ICollection<Wallet>? Wallets { get; set; }
        public ICollection<Stock>? Stocks { get; set; }
        public ICollection<Order>? Orders { get; set; }
        public ICollection<Transaction>? Transactions { get; set; }
        public ICollection<FutureTrading>? FutureTradings { get; set; }
        public ICollection<Dividend>? Dividends { get; set; }
        public ICollection<FundInvestment>? FundInvestments { get; set; }
        public ICollection<SpotTrading>? SpotTradings { get; set; }
    }
}
