using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Dividend
    {
        [Key]
        public int DividendId { get; set; }
        public int? StockId { get; set; }
        public int? UserId { get; set; }
        public int Amount { get; set; }
        public DateTime ReceivedDate { get; set; }

        public User? User { get; set; }
        public Stock? Stock { get; set; }
    }
}
