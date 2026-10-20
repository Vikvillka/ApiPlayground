using Microsoft.EntityFrameworkCore;

namespace ApiPlayground.Api.Data;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
}
