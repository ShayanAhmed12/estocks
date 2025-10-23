using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class FutureContract
    {
        [Key]
        public int ContractId { get; set; }

        public int StockId { get; set; }

        public DateTime ExpiryDate { get; set; }

        public int ContractPrice { get; set; }

        [Required]
        public string ContractType { get; set; } = string.Empty;

        // Navigation properties
        public Stock? Stock { get; set; }

        public ICollection<FutureTrading>? FutureTradings { get; set; }
    }
}
