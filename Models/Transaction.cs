using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Transaction
    {
        [Key]
        public int TransactionId { get; set; }

        public int? WalletId { get; set; }
        public int? UserId { get; set; }
        public int? StockId { get; set; }

        public int Quantity { get; set; }

        [Required]
        public string TransactionType { get; set; } = string.Empty;

        // Navigation properties
        public Wallet? Wallet { get; set; }
        public User? User { get; set; }
        public Stock? Stock { get; set; }
    }
}
