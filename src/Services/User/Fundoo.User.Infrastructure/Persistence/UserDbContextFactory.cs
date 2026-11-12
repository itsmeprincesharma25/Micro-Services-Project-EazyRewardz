using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fundoo.User.Infrastructure.Persistence;

public sealed class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__UserDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                "Host=localhost;Port=5432;Database=FundooUserDb;Username=postgres";
        }

        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new UserDbContext(options);
    }
}
