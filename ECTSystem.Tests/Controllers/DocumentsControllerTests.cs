using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="DocumentsController"/>, the OData controller that provides
/// read-only access to document metadata (file name, content type, size) in the ECT System API.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="DocumentsController"/> intentionally omits binary file content from its
/// responses; upload and download of file bytes are handled by a separate
/// <c>DocumentFilesController</c>. These tests verify the metadata-query endpoints only.
/// </para>
/// <para>
/// Each test instance creates an isolated in-memory EF Core database. Tests are organized
/// by controller action: <c>Get</c> (collection) and <c>Get</c> (single by key),
/// covering both success and not-found scenarios.
/// </para>
/// </remarks>
public class DocumentsControllerTests : ControllerTestBase
{
    /// <summary>In-memory database options shared across seed, act, and verify phases.</summary>
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    /// <summary>Mocked context factory returning <see cref="EctDbContext"/> instances backed by the in-memory store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    /// <summary>Mocked logging service injected into the controller.</summary>
    private readonly Mock<ILoggingService> _mockLog;
    /// <summary>System under test - the <see cref="DocumentsController"/> instance.</summary>
    private readonly DocumentsController _sut;

    /// <summary>
    /// Initializes the in-memory database, configures mocked dependencies, and creates
    /// the <see cref="DocumentsController"/> with a fake authenticated user context.
    /// </summary>
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

    /// <summary>
    /// Seeds a <see cref="LineOfDutyDocument"/> into the in-memory database for test arrangement.
    /// </summary>
    /// <param name="doc">The document entity to persist, including required metadata fields.</param>
    private async Task SeedDocumentAsync(LineOfDutyDocument doc)
    {
        await using var context = new EctDbContext(_dbOptions);
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
    }

    // ──────────────────────────── Get (collection) ───────────────────────────

    /// <summary>
    /// Verifies that <see cref="DocumentsController.Get()"/> returns an
    /// <see cref="OkObjectResult"/> wrapping an <see cref="IQueryable{LineOfDutyDocument}"/>.
    /// </summary>
    [Fact]
    public async Task Get_ReturnsOkWithDocumentsQueryable()
    {
        var result = await _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ──────────────────────────── Get (single) ───────────────────────────────

    /// <summary>
    /// Verifies that <see cref="DocumentsController.Get(int)"/> returns <see cref="OkObjectResult"/>
    /// with the matching <see cref="LineOfDutyDocument"/> when the document exists in the store.
    /// </summary>
    [Fact]
    public async Task Get_WhenDocumentFound_ReturnsOkWithDocument()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "form.pdf", ContentType = "application/pdf" });

        var result = await _sut.Get(key: 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var doc = Assert.IsType<LineOfDutyDocument>(ok.Value);
        Assert.Equal("form.pdf", doc.FileName);
    }

    /// <summary>
    /// Verifies that <c>Get</c> returns <see cref="NotFoundResult"/> when no document
    /// with the specified key exists.
    /// </summary>
    [Fact]
    public async Task Get_WhenDocumentNotFound_ReturnsNotFound()
    {
        var result = await _sut.Get(key: 99);

        Assert.IsType<NotFoundResult>(result);
    }
}
