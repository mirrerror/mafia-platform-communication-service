using MafiaCommunicationService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Data;

public class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatMessageEntity> Messages { get; set; }
    public DbSet<LobbyEntity> Lobbies { get; set; }
    public DbSet<PrivateChannelEntity> PrivateChannels { get; set; }
    public DbSet<PrivateChannelMemberEntity> PrivateChannelMembers { get; set; }
    public DbSet<Announcement> Announcements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<ChatMessageEntity>()
            .HasIndex(m => new { m.LobbyId, m.Timestamp });
        
        modelBuilder.Entity<LobbyEntity>()
            .HasMany(l => l.PrivateChannels)
            .WithOne(pc => pc.Lobby)
            .HasForeignKey(pc => pc.LobbyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PrivateChannelEntity>()
            .HasMany(pc => pc.Members)
            .WithOne(pcm => pcm.PrivateChannel)
            .HasForeignKey(pcm => pcm.PrivateChannelId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<PrivateChannelEntity>()
            .HasIndex(pc => new { pc.LobbyId, pc.Name }).IsUnique();

        modelBuilder.Entity<PrivateChannelMemberEntity>()
            .HasIndex(pcm => new { pcm.PrivateChannelId, pcm.MemberId }).IsUnique();
    }
}