using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        public int? UserId { get; set; }
        public int? StockId { get; set; }

        [Required]
        public string OrderType { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public int Price { get; set; }
        public bool OrderStatus { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public Stock? Stock { get; set; }
    }
}


