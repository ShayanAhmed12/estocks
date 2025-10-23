using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class FutureTrading
    {
        [Key]
        public int FutureTradingId { get; set; }
        public int? ContractId { get; set; }
        public int? UserId { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public DateTime TradeTime { get; set; }

        public User? User { get; set; }
        public FutureContract? Contract { get; set; }
    }
}
