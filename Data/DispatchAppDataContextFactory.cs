using DispatchApp.Server.data;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DispatchApp.Server.Data
{
    public class DispatchAppDataContextFactory : IDesignTimeDbContextFactory<DispatchDbContext>
    {
        public DispatchDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory()))
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true).Build();

            return new DispatchDbContext(config.GetConnectionString("ConStr"));
        }
    }

}
