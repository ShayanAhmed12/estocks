using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Wallet
    {
        [Key]
        public int WalletId { get; set; }

        public int UserId { get; set; }

        public int Balance { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Navigation property — made nullable to avoid CS8618 warning
        public User? User { get; set; }

        // Navigation property — nullable collection
        public ICollection<Transaction>? Transactions { get; set; }
    }
}
