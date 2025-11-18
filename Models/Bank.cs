using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Bank
    {





        [Key]
        public int BankId { get; set; }

        public int? UserId { get; set; }

        [Required]
        public string BankName { get; set; } = string.Empty;
        public int AccountNumber { get; set; }
        public string AccountTitle { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public User? User { get; set; }


    }

}

