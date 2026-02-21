using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class DataServiceNotificationTests : DataServiceTestBase
{
    // ─────────────────────── GetNotificationsByCaseIdAsync ────────────────────────

    [Fact]
    public async Task GetNotificationsByCaseIdAsync_ReturnsNotificationsForCase()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        ctx.Cases.Add(BuildCase(2, "CASE-0002"));
        await ctx.SaveChangesAsync();

        ctx.Notifications.AddRange(
            new Notification { LineOfDutyCaseId = 1, Title = "N1", Message = "msg1" },
            new Notification { LineOfDutyCaseId = 1, Title = "N2", Message = "msg2" },
            new Notification { LineOfDutyCaseId = 2, Title = "N3", Message = "msg3" }
        );
        await ctx.SaveChangesAsync();

        var result = await Sut.GetNotificationsByCaseIdAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, n => Assert.Equal(1, n.LineOfDutyCaseId));
    }

    [Fact]
    public async Task GetNotificationsByCaseIdAsync_WhenNoNotifications_ReturnsEmptyList()
    {
        var result = await Sut.GetNotificationsByCaseIdAsync(99);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNotificationsByCaseIdAsync_ReturnsNotificationsOrderedByCreatedDateDescending()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        // Insert first notification
        var n1 = new Notification { LineOfDutyCaseId = 1, Title = "Older", Message = "msg" };
        seedCtx.Notifications.Add(n1);
        await seedCtx.SaveChangesAsync();

        // Insert second notification
        var n2 = new Notification { LineOfDutyCaseId = 1, Title = "Newer", Message = "msg" };
        seedCtx.Notifications.Add(n2);
        await seedCtx.SaveChangesAsync();

        // Force distinct timestamps by updating CreatedDate on the older notification
        n1.CreatedDate = DateTime.UtcNow.AddMinutes(-5);
        await seedCtx.SaveChangesAsync();

        var result = await Sut.GetNotificationsByCaseIdAsync(1);

        Assert.Equal(2, result.Count);
        // Newer (n2) should come first since its CreatedDate is more recent
        Assert.True(result[0].CreatedDate >= result[1].CreatedDate);
    }

    // ──────────────────────────── AddNotificationAsync ───────────────────────────

    [Fact]
    public async Task AddNotificationAsync_AddsAndReturnsNotification()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var notification = new Notification
        {
            LineOfDutyCaseId = 1,
            Title            = "Case Status Update",
            Message          = "Your LOD case has been reviewed.",
            Recipient        = "john.doe@mil.af.us",
            NotificationType = "StatusChange",
            IsRead           = false
        };

        var result = await Sut.AddNotificationAsync(notification);

        Assert.True(result.Id > 0);
        Assert.Equal("Case Status Update", result.Title);
        Assert.False(result.IsRead);
    }

    [Fact]
    public async Task AddNotificationAsync_PersistsToDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        await Sut.AddNotificationAsync(new Notification
        {
            LineOfDutyCaseId = 1,
            Title            = "PersistTest",
            Message          = "Test message"
        });

        await using var verifyCtx = CreateSeedContext();
        Assert.True(await verifyCtx.Notifications.AnyAsync(n => n.Title == "PersistTest"));
    }

    [Theory]
    [InlineData("StatusChange")]
    [InlineData("Reminder")]
    [InlineData("ActionRequired")]
    [InlineData("Information")]
    public async Task AddNotificationAsync_StoringDifferentTypes_AllPersist(string notificationType)
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var result = await Sut.AddNotificationAsync(new Notification
        {
            LineOfDutyCaseId = 1,
            Title            = $"Test {notificationType}",
            Message          = "Message",
            NotificationType = notificationType
        });

        Assert.Equal(notificationType, result.NotificationType);
        Assert.True(result.Id > 0);
    }

    // ─────────────────────────────── MarkAsReadAsync ─────────────────────────────

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationExists_ReturnsTrueAndSetsReadFlag()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var notification = new Notification
        {
            LineOfDutyCaseId = 1,
            Title            = "Unread",
            Message          = "Please read me.",
            IsRead           = false
        };
        seedCtx.Notifications.Add(notification);
        await seedCtx.SaveChangesAsync();

        var result = await Sut.MarkAsReadAsync(notification.Id);

        Assert.True(result);

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Notifications.FindAsync(notification.Id);
        Assert.True(saved.IsRead);
        Assert.NotNull(saved.ReadDate);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationNotFound_ReturnsFalse()
    {
        var result = await Sut.MarkAsReadAsync(9999);

        Assert.False(result);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenMarkedRead_SetsReadDateToUtcNow()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var notification = new Notification { LineOfDutyCaseId = 1, Title = "T", Message = "M" };
        seedCtx.Notifications.Add(notification);
        await seedCtx.SaveChangesAsync();

        var before = DateTime.UtcNow;
        await Sut.MarkAsReadAsync(notification.Id);
        var after = DateTime.UtcNow;

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Notifications.FindAsync(notification.Id);
        Assert.NotNull(saved.ReadDate);
        Assert.True(saved.ReadDate >= before.AddSeconds(-1));
        Assert.True(saved.ReadDate <= after.AddSeconds(1));
    }
}
