using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;

namespace WebApplication2.Data
{
    public class EstocksDbContext : DbContext
    {
        public EstocksDbContext(DbContextOptions<EstocksDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<FutureContract> FutureContracts { get; set; }
        public DbSet<FutureTrading> FutureTradings { get; set; }
        public DbSet<SpotTrading> SpotTradings { get; set; }
        public DbSet<Dividend> Dividends { get; set; }
        public DbSet<Fund> Funds { get; set; }
        public DbSet<FundInvestment> FundInvestments { get; set; }

        // Added Bank DbSet
        public DbSet<Bank> Banks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Foreign Key Relationships

            // User -> Wallets
            modelBuilder.Entity<Wallet>()
                .HasOne(w => w.User)
                .WithMany(u => u.Wallets)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User -> Stocks
            modelBuilder.Entity<Stock>()
                .HasOne(s => s.User)
                .WithMany(u => u.Stocks)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> Orders
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock -> Orders
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Stock)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.StockId)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> Transactions
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Wallet -> Transactions
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock -> Transactions
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Stock)
                .WithMany(s => s.Transactions)
                .HasForeignKey(t => t.StockId)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock -> FutureContracts
            modelBuilder.Entity<FutureContract>()
                .HasOne(f => f.Stock)
                .WithMany(s => s.FutureContracts)
                .HasForeignKey(f => f.StockId)
                .OnDelete(DeleteBehavior.Cascade);

            // FutureContract -> FutureTrading
            modelBuilder.Entity<FutureTrading>()
                .HasOne(ft => ft.Contract)
                .WithMany(fc => fc.FutureTradings)
                .HasForeignKey(ft => ft.ContractId)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> FutureTrading
            modelBuilder.Entity<FutureTrading>()
                .HasOne(ft => ft.User)
                .WithMany(u => u.FutureTradings)
                .HasForeignKey(ft => ft.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock -> SpotTrading
            modelBuilder.Entity<SpotTrading>()
                .HasOne(st => st.Stock)
                .WithMany(s => s.SpotTradings)
                .HasForeignKey(st => st.StockId)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> SpotTrading
            modelBuilder.Entity<SpotTrading>()
                .HasOne(st => st.User)
                .WithMany(u => u.SpotTradings)
                .HasForeignKey(st => st.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Stock -> Dividends
            modelBuilder.Entity<Dividend>()
                .HasOne(d => d.Stock)
                .WithMany(s => s.Dividends)
                .HasForeignKey(d => d.StockId)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> Dividends
            modelBuilder.Entity<Dividend>()
                .HasOne(d => d.User)
                .WithMany(u => u.Dividends)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Fund -> FundInvestments
            modelBuilder.Entity<FundInvestment>()
                .HasOne(fi => fi.Fund)
                .WithMany(f => f.FundInvestments)
                .HasForeignKey(fi => fi.FundId)
                .OnDelete(DeleteBehavior.Cascade);

            // User -> FundInvestments
            modelBuilder.Entity<FundInvestment>()
                .HasOne(fi => fi.User)
                .WithMany(u => u.FundInvestments)
                .HasForeignKey(fi => fi.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> Banks (new)
            modelBuilder.Entity<Bank>()
                .HasOne(b => b.User)
                .WithMany(u => u.Banks)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Primary keys (optional, EF Core detects them automatically via [Key])
            modelBuilder.Entity<User>().HasKey(u => u.UserId);
            modelBuilder.Entity<Wallet>().HasKey(w => w.WalletId);
            modelBuilder.Entity<Stock>().HasKey(s => s.StockId);
            modelBuilder.Entity<Order>().HasKey(o => o.OrderId);
            modelBuilder.Entity<Transaction>().HasKey(t => t.TransactionId);
            modelBuilder.Entity<FutureContract>().HasKey(f => f.ContractId);
            modelBuilder.Entity<FutureTrading>().HasKey(ft => ft.FutureTradingId);
            modelBuilder.Entity<SpotTrading>().HasKey(st => st.TradeId);
            modelBuilder.Entity<Dividend>().HasKey(d => d.DividendId);
            modelBuilder.Entity<Fund>().HasKey(fu => fu.FundId);
            modelBuilder.Entity<FundInvestment>().HasKey(fi => fi.InvestmentId);
            modelBuilder.Entity<Bank>().HasKey(b => b.BankId);
        }
    }
}