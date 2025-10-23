using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class FundInvestment
    {
        [Key]
        public int InvestmentId { get; set; }

        public int FundId { get; set; }
        public int? UserId { get; set; }

        public int Amount { get; set; }
        public int BuyPrice { get; set; }
        public DateTime BuyDate { get; set; }
        public DateTime Maturity { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public Fund? Fund { get; set; }
    }
}
