using Microsoft.EntityFrameworkCore;
using InteractiveMapGame.Models;

namespace InteractiveMapGame.Data
{
    public class MapGameDbContext : DbContext
    {
        public MapGameDbContext(DbContextOptions<MapGameDbContext> options) : base(options)
        {
        }

        public DbSet<MapObject> MapObjects { get; set; }
        public DbSet<PlayerProgress> PlayerProgress { get; set; }
        public DbSet<InteractionLog> InteractionLogs { get; set; }
        public DbSet<Admin> Admins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure MapObject
            modelBuilder.Entity<MapObject>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Era).HasMaxLength(100);
                entity.Property(e => e.Manufacturer).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.ImageUrl).HasMaxLength(200);
                entity.Property(e => e.ModelUrl).HasMaxLength(200);
                entity.Property(e => e.Video360Url).HasMaxLength(200);
                
                // Indexes for performance
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsUnlocked);
                entity.HasIndex(e => new { e.X, e.Y, e.Z });
            });

            // Configure PlayerProgress
            modelBuilder.Entity<PlayerProgress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PlayerId).IsRequired().HasMaxLength(64);
                entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
                entity.Property(e => e.UnlockedObjects).HasColumnType("nvarchar(max)");
                entity.Property(e => e.CompletedQuests).HasColumnType("nvarchar(max)");
                entity.Property(e => e.PlayerPreferences).HasColumnType("nvarchar(max)");
                
                entity.HasIndex(e => e.PlayerId);
                entity.HasIndex(e => e.SessionId);
            });

            // Configure InteractionLog
            modelBuilder.Entity<InteractionLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PlayerId).IsRequired().HasMaxLength(64);
                entity.Property(e => e.InteractionType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.InteractionData).HasMaxLength(1000);
                entity.Property(e => e.LLMPrompt).HasColumnType("nvarchar(max)");
                entity.Property(e => e.LLMResponse).HasColumnType("nvarchar(max)");
                
                entity.HasIndex(e => e.PlayerId);
                entity.HasIndex(e => e.MapObjectId);
                entity.HasIndex(e => e.Timestamp);
            });

            // Configure Admin
            modelBuilder.Entity<Admin>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Email).HasMaxLength(200);
                
                entity.HasIndex(e => e.Username).IsUnique();
            });
        }
    }
}
