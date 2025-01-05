using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using GolfClubSystem.Context;

namespace GolfClubSystem.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<MyDbContext>
    {
        public MyDbContext CreateDbContext(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Set the base directory to current directory
                .AddJsonFile("appsettings.json") // Read the connection string from appsettings.json
                .Build();

            // Get the connection string
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<MyDbContext>();

            // Configure DbContext with MySQL
            optionsBuilder.UseMySql(connectionString, ServerVersion.Parse("8.0.37-mysql"));

            return new MyDbContext(optionsBuilder.Options);
        }
    }
}