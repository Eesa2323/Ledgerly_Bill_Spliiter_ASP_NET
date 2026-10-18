using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BillSplitter;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
    // EF uses this factory when creating migrations outside the running web host.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=BillSplitterDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True")
            .Options;
        return new AppDbContext(options);
    }
}