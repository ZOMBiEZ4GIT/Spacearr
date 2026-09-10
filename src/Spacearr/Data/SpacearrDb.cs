using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Spacearr.Data.Entities;

namespace Spacearr.Data;

public sealed class SpacearrDb : DbContext
{
    public SpacearrDb(DbContextOptions<SpacearrDb> options) : base(options) { }

    public DbSet<ArrInstance> ArrInstances => Set<ArrInstance>();
    public DbSet<PathMapping> PathMappings => Set<PathMapping>();
    public DbSet<RootFolder> RootFolders => Set<RootFolder>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<ActionLog> ActionLogs => Set<ActionLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Setting>().HasKey(s => s.Key);
        b.Entity<User>().Property(u => u.Username).UseCollation("NOCASE");
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<User>().HasIndex(u => u.ApiKey).IsUnique();
        b.Entity<MediaFile>().HasIndex(f => f.Path).IsUnique();
        b.Entity<MediaFile>().HasIndex(f => f.RootFolderId);
        b.Entity<MediaItem>().HasIndex(i => new { i.ArrInstanceId, i.Kind, i.ExternalId }).IsUnique();
        b.Entity<MediaItem>().HasIndex(i => i.MediaFileId);
        b.Entity<MediaItem>().HasOne(i => i.MediaFile).WithMany().HasForeignKey(i => i.MediaFileId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<MediaItem>().HasOne(i => i.ArrInstance).WithMany().HasForeignKey(i => i.ArrInstanceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PathMapping>().HasOne(m => m.ArrInstance).WithMany(i => i.PathMappings).HasForeignKey(m => m.ArrInstanceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Job>().HasIndex(j => j.QueuedAt);
        b.Entity<ActionLog>().HasIndex(a => a.At);

        var utc = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
                else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNullable);
            }
        }
    }
}
