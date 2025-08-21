// Plik: src/backend/Data/BmsDbContext.cs
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class BmsDbContext : DbContext
{
    public BmsDbContext(DbContextOptions<BmsDbContext> options) : base(options)
    {
    }

    public DbSet<Chamber> Chambers { get; set; }
}