using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Controllers;

public class DocumentsControllerTests : ControllerTestBase
{
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly Mock<ILoggingService> _mockLog;
    private readonly DocumentsController _sut;

    public DocumentsControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();
        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));
        _mockContextFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(_dbOptions));

        _mockLog = new Mock<ILoggingService>();

        _sut = new DocumentsController(_mockContextFactory.Object, _mockLog.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    private async Task SeedDocumentAsync(LineOfDutyDocument doc)
    {
        await using var context = new EctDbContext(_dbOptions);
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
    }

    // ──────────────────────────── Get (collection) ───────────────────────────

    [Fact]
    public async Task Get_ReturnsOkWithDocumentsQueryable()
    {
        var result = await _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ──────────────────────────── Get (single) ───────────────────────────────

    [Fact]
    public async Task Get_WhenDocumentFound_ReturnsOkWithDocument()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "form.pdf", ContentType = "application/pdf" });

        var result = await _sut.Get(key: 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var doc = Assert.IsType<LineOfDutyDocument>(ok.Value);
        Assert.Equal("form.pdf", doc.FileName);
    }

    [Fact]
    public async Task Get_WhenDocumentNotFound_ReturnsNotFound()
    {
        var result = await _sut.Get(key: 99);

        Assert.IsType<NotFoundResult>(result);
    }


}
