using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebApplication2.Data
{
    public class EstocksDbContextFactory : IDesignTimeDbContextFactory<EstocksDbContext>
    {
        public EstocksDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EstocksDbContext>();
            var connectionString = "Server=localhost;Port=3306;Database=estocks;User=root;Password=Fast1234;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            return new EstocksDbContext(optionsBuilder.Options);
        }
    }
}
