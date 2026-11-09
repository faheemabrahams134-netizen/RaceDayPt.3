using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RaceEvent> Events => Set<RaceEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<EventEnrolment> Enrolments => Set<EventEnrolment>();
    public DbSet<RaceResult> Results => Set<RaceResult>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("User");
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.ProfilePictureBlob).HasMaxLength(200);
        });

        b.Entity<RaceEvent>(e =>
        {
            e.ToTable("Event");
            e.HasKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.BannerBlob).HasMaxLength(200);
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("Category");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(255);
            e.Property(x => x.CategoryType).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventEnrolment>(e =>
        {
            e.ToTable("EventEnrolment");
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            // A participant can enter an event once (in exactly one category).
            e.HasIndex(x => new { x.EventId, x.ParticipantId }).IsUnique();
            // Restrict avoids SQL Server "multiple cascade paths" errors.
            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RaceResult>(e =>
        {
            e.ToTable("Result");
            e.HasKey(x => x.ResultId);
            e.Property(x => x.FinishTime).HasColumnType("time(0)");
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<RaceResult>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
