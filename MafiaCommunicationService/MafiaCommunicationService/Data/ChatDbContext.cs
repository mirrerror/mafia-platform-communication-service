using MafiaCommunicationService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Data;

public class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatMessageEntity> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ChatMessageEntity>()
            .HasIndex(m => new { m.LobbyId, m.Timestamp });
    }
}