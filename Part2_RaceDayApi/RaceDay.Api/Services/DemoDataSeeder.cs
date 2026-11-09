using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Optional demo data (enabled with Seed:DemoData=true) so the app is usable straight after `docker compose up`.
/// Only runs against an empty database. All demo users have the password Password123!
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordService hasher)
    {
        if (await db.Users.AnyAsync()) return;

        var hash = hasher.Hash("Password123!");
        var organiser = new AppUser { FullName = "Lerato Mokoena", Email = "lerato.organiser@raceday.co.za", PasswordHash = hash, Role = Roles.Organiser, Phone = "0821234567" };
        var sipho = new AppUser { FullName = "Sipho Nkosi", Email = "sipho.runner@raceday.co.za", PasswordHash = hash, Role = Roles.Participant, Phone = "0731112222", DateOfBirth = new DateOnly(1998, 3, 14) };
        var amy = new AppUser { FullName = "Amy Fischer", Email = "amy.runner@raceday.co.za", PasswordHash = hash, Role = Roles.Participant, Phone = "0745556666", DateOfBirth = new DateOnly(2007, 9, 2) };
        var thandi = new AppUser { FullName = "Thandi Dlamini", Email = "thandi.runner@raceday.co.za", PasswordHash = hash, Role = Roles.Participant, Phone = "0769990000", DateOfBirth = new DateOnly(1994, 6, 21) };
        db.Users.AddRange(organiser, sipho, amy, thandi);
        await db.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        var past = new RaceEvent
        {
            OrganiserId = organiser.UserId, Name = "Sea Point Sunrise 10K",
            Description = "A flat, fast sunrise 10K along the Sea Point promenade.",
            EventDate = today.AddDays(-21).AddHours(6), Location = "Sea Point, Cape Town", DistanceKm = 10m, EventType = EventType.Run
        };
        var trail = new RaceEvent
        {
            OrganiserId = organiser.UserId, Name = "Table Mountain Trail Run",
            Description = "Scenic trail run along the lower slopes of Table Mountain.",
            EventDate = today.AddDays(14).AddHours(7), Location = "Cape Town, Western Cape", DistanceKm = 21.1m, EventType = EventType.Run
        };
        var cycle = new RaceEvent
        {
            OrganiserId = organiser.UserId, Name = "Winelands Cycle Classic",
            Description = "Road cycle through the Stellenbosch winelands.",
            EventDate = today.AddDays(30).AddHours(6).AddMinutes(30), Location = "Stellenbosch, Western Cape", DistanceKm = 60m, EventType = EventType.Cycle
        };
        var walk = new RaceEvent
        {
            OrganiserId = organiser.UserId, Name = "Sea Point Family Walk",
            Description = "Relaxed family walk along the Sea Point promenade.",
            EventDate = today.AddDays(45).AddHours(8), Location = "Sea Point, Cape Town", DistanceKm = 5m, EventType = EventType.Walk
        };
        db.Events.AddRange(past, trail, cycle, walk);
        await db.SaveChangesAsync();

        var pastUnder20 = new Category { EventId = past.EventId, Name = "Under 20", Description = "Runners aged 19 and under", CategoryType = CategoryType.Age, MaxAge = 19 };
        var pastSenior = new Category { EventId = past.EventId, Name = "Senior", Description = "Runners aged 20 and over", CategoryType = CategoryType.Age, MinAge = 20 };
        var trailUnder20 = new Category { EventId = trail.EventId, Name = "Under 20", Description = "Runners aged 19 and under", CategoryType = CategoryType.Age, MaxAge = 19 };
        var trailSenior = new Category { EventId = trail.EventId, Name = "Senior", Description = "Runners aged 20 and over", CategoryType = CategoryType.Age, MinAge = 20 };
        var km60 = new Category { EventId = cycle.EventId, Name = "60km", Description = "Full classic route", CategoryType = CategoryType.Distance, DistanceKm = 60m };
        var km30 = new Category { EventId = cycle.EventId, Name = "30km", Description = "Short route", CategoryType = CategoryType.Distance, DistanceKm = 30m };
        var km5 = new Category { EventId = walk.EventId, Name = "5km", Description = "Standard family walk", CategoryType = CategoryType.Distance, DistanceKm = 5m };
        db.Categories.AddRange(pastUnder20, pastSenior, trailUnder20, trailSenior, km60, km30, km5);
        await db.SaveChangesAsync();

        var e1 = new EventEnrolment { EventId = past.EventId, ParticipantId = sipho.UserId, CategoryId = pastSenior.CategoryId, Status = EnrolmentStatus.Confirmed };
        var e2 = new EventEnrolment { EventId = past.EventId, ParticipantId = thandi.UserId, CategoryId = pastSenior.CategoryId, Status = EnrolmentStatus.Confirmed };
        var e3 = new EventEnrolment { EventId = past.EventId, ParticipantId = amy.UserId, CategoryId = pastUnder20.CategoryId, Status = EnrolmentStatus.Confirmed };
        var e4 = new EventEnrolment { EventId = trail.EventId, ParticipantId = sipho.UserId, CategoryId = trailSenior.CategoryId, Status = EnrolmentStatus.Pending };
        var e5 = new EventEnrolment { EventId = trail.EventId, ParticipantId = amy.UserId, CategoryId = trailUnder20.CategoryId, Status = EnrolmentStatus.Confirmed };
        var e6 = new EventEnrolment { EventId = cycle.EventId, ParticipantId = sipho.UserId, CategoryId = km60.CategoryId, Status = EnrolmentStatus.Pending };
        db.Enrolments.AddRange(e1, e2, e3, e4, e5, e6);
        await db.SaveChangesAsync();

        db.Results.AddRange(
            new RaceResult { EnrolmentId = e1.EnrolmentId, FinishTime = new TimeSpan(0, 48, 30), FinishPosition = 2 },
            new RaceResult { EnrolmentId = e2.EnrolmentId, FinishTime = new TimeSpan(0, 46, 12), FinishPosition = 1 },
            new RaceResult { EnrolmentId = e3.EnrolmentId, FinishTime = new TimeSpan(0, 55, 10), FinishPosition = 1 });
        await db.SaveChangesAsync();
    }
}
