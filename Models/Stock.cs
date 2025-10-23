using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Stock
    {
        [Key]
        public int StockId { get; set; }

        public int Price { get; set; }

        [Required]
        public string CompanyName { get; set; } = string.Empty;

        public int? UserId { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public ICollection<FutureContract>? FutureContracts { get; set; }
        public ICollection<Order>? Orders { get; set; }
        public ICollection<Transaction>? Transactions { get; set; }
        public ICollection<Dividend>? Dividends { get; set; }
        public ICollection<SpotTrading>? SpotTradings { get; set; }
    }
}
