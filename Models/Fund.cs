using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace WebApplication2.Models
{
    public class Fund
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FundId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FundName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string FundType { get; set; } = string.Empty;

        [Required]
        public int NetAssetValue { get; set; }

        [Required]
        [MaxLength(100)]
        public string Consolidator { get; set; } = string.Empty;

        // Navigation property for related investments
        public ICollection<FundInvestment>? FundInvestments { get; set; }
    }
}
