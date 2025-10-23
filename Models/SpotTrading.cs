using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class SpotTrading
    {
        [Key]
        public int TradeId { get; set; }
        public int? UserId { get; set; }
        public int? StockId { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public DateTime TradeTime { get; set; }

        public User? User { get; set; }
        public Stock? Stock { get; set; }
    }
}
