using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="DocumentsController"/>, the OData controller managing
/// LineOfDutyDocument CRUD operations.
/// Uses SQLite in-memory provider (not EF Core InMemory) because the Delete method
/// relies on ExecuteDeleteAsync which is not supported by the InMemory provider.
/// </summary>
public class DocumentsControllerTests : ControllerTestBase, IDisposable
{
    private readonly Mock<ILoggingService> _mockLog;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly Mock<IBlobStorageService> _mockBlobStorage;
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    private readonly SqliteConnection _connection;
    private readonly DocumentsController _sut;

    public DocumentsControllerTests()
    {
        // SQLite in-memory requires a shared open connection for all contexts
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema using test context that replaces SQL Server types
        using (var schemaCtx = new SqliteEctDbContext(_dbOptions))
        {
            schemaCtx.Database.EnsureCreated();
        }

        // Seed a default Member and Case so FK references are valid
        using (var seedCtx = new SqliteEctDbContext(_dbOptions))
        {
            seedCtx.Members.Add(new Member
            {
                Id = 1, FirstName = "John", LastName = "Doe",
                Rank = "SSgt", Unit = "99 ABW"
            });
            seedCtx.SaveChanges();

            seedCtx.Cases.Add(BuildCase(1));
            seedCtx.SaveChanges();
        }

        _mockLog = new Mock<ILoggingService>();
        _mockBlobStorage = new Mock<IBlobStorageService>();
        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();

        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SqliteEctDbContext(_dbOptions));

        _sut = new DocumentsController(_mockContextFactory.Object, _mockLog.Object, CreatePdfService(), _mockBlobStorage.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private EctDbContext CreateSeedContext() => new SqliteEctDbContext(_dbOptions);

    /// <summary>
    /// EctDbContext subclass that replaces SQL Server–specific column types and
    /// default value expressions with SQLite-compatible equivalents.
    /// </summary>
    private class SqliteEctDbContext(DbContextOptions<EctDbContext> options) : EctDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Replace GETUTCDATE() with SQLite equivalent
            modelBuilder.Entity<WorkflowModule>()
                .Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
            modelBuilder.Entity<WorkflowModule>()
                .Property(e => e.ModifiedDate).HasDefaultValueSql("datetime('now')");

            modelBuilder.Entity<WorkflowType>()
                .Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
            modelBuilder.Entity<WorkflowType>()
                .Property(e => e.ModifiedDate).HasDefaultValueSql("datetime('now')");

            modelBuilder.Entity<WorkflowStateLookup>()
                .Property(e => e.CreatedDate).HasDefaultValueSql("datetime('now')");
            modelBuilder.Entity<WorkflowStateLookup>()
                .Property(e => e.ModifiedDate).HasDefaultValueSql("datetime('now')");
        }
    }

    private LineOfDutyDocument BuildDocument(int caseId = 1) => new()
    {
        LineOfDutyCaseId = caseId,
        DocumentType = DocumentType.Miscellaneous,
        FileName = "test-report.pdf",
        ContentType = "application/pdf",
        FileSize = 1024,
        BlobPath = "cases/1/test-blob-path",
        UploadDate = DateTime.UtcNow,
        Description = "Unit test document"
    };

    private int SeedDocument(int caseId = 1)
    {
        using var ctx = CreateSeedContext();
        var doc = BuildDocument(caseId);
        ctx.Documents.Add(doc);
        ctx.SaveChanges();
        return doc.Id;
    }

    // ────────────────────────── Get (collection) ──────────────────────────────

    [Fact]
    public async Task Get_ReturnsOkWithQueryable()
    {
        SeedDocument();

        var result = await _sut.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    // ────────────────────────── Get (key) ──────────────────────────────

    [Fact]
    public async Task Get_ByKey_ReturnsSingleResult()
    {
        var docId = SeedDocument();

        var result = await _sut.Get(docId);

        Assert.IsType<SingleResult<LineOfDutyDocument>>(result);

        // Materialize the deferred query to verify the document is returned
        var document = await result.Queryable.FirstOrDefaultAsync();
        Assert.NotNull(document);
        Assert.Equal("test-report.pdf", document.FileName);
    }

    [Fact]
    public async Task Get_ByKey_WhenNotFound_ReturnsEmptySingleResult()
    {
        var result = await _sut.Get(999);

        Assert.IsType<SingleResult<LineOfDutyDocument>>(result);
        var document = await result.Queryable.FirstOrDefaultAsync();
        Assert.Null(document);
    }

    // ────────────────────────── Patch ──────────────────────────────

    [Fact]
    public async Task Patch_UpdatesExistingDocument()
    {
        var docId = SeedDocument();

        var delta = new Delta<LineOfDutyDocument>();
        delta.TrySetPropertyValue(nameof(LineOfDutyDocument.Description), "Updated description");
        delta.TrySetPropertyValue(nameof(LineOfDutyDocument.DocumentType), DocumentType.MilitaryMedicalDocumentation);

        var result = await _sut.Patch(docId, delta);

        var updated = Assert.IsType<UpdatedODataResult<LineOfDutyDocument>>(result);
        Assert.Equal("Updated description", updated.Entity.Description);
        Assert.Equal(DocumentType.MilitaryMedicalDocumentation, updated.Entity.DocumentType);
    }

    [Fact]
    public async Task Patch_PreservesUnchangedFields()
    {
        var docId = SeedDocument();

        var delta = new Delta<LineOfDutyDocument>();
        delta.TrySetPropertyValue(nameof(LineOfDutyDocument.Description), "New description");

        var result = await _sut.Patch(docId, delta);

        var updated = Assert.IsType<UpdatedODataResult<LineOfDutyDocument>>(result);
        Assert.Equal("test-report.pdf", updated.Entity.FileName);
        Assert.Equal("application/pdf", updated.Entity.ContentType);
    }

    [Fact]
    public async Task Patch_WhenNotFound_ReturnsNotFound()
    {
        var delta = new Delta<LineOfDutyDocument>();
        delta.TrySetPropertyValue(nameof(LineOfDutyDocument.Description), "New");

        var result = await _sut.Patch(999, delta);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, obj.StatusCode);
        Assert.IsType<ProblemDetails>(obj.Value);
    }

    [Fact]
    public async Task Patch_WhenNullDelta_ReturnsBadRequest()
    {
        var result = await _sut.Patch(1, null!);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.IsType<ValidationProblemDetails>(obj.Value);
    }

    // ────────────────────────── Put ──────────────────────────────

    [Fact]
    public async Task Put_ReplacesExistingDocument()
    {
        var docId = SeedDocument();

        // Read back the persisted RowVersion for concurrency check
        using var ctx = CreateSeedContext();
        var persisted = await ctx.Documents.FindAsync(docId);

        var replacement = BuildDocument();
        replacement.Id = docId;
        replacement.FileName = "replaced-report.pdf";
        replacement.Description = "Fully replaced document";
        replacement.RowVersion = persisted!.RowVersion;

        var result = await _sut.Put(docId, replacement);

        var updated = Assert.IsType<UpdatedODataResult<LineOfDutyDocument>>(result);
        Assert.Equal("replaced-report.pdf", updated.Entity.FileName);
        Assert.Equal("Fully replaced document", updated.Entity.Description);
    }

    [Fact]
    public async Task Put_WhenNotFound_ReturnsNotFound()
    {
        var document = BuildDocument();
        document.Id = 999;

        var result = await _sut.Put(999, document);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, obj.StatusCode);
        Assert.IsType<ProblemDetails>(obj.Value);
    }

    [Fact]
    public async Task Put_WhenKeyMismatch_ReturnsBadRequest()
    {
        var docId = SeedDocument();

        var document = BuildDocument();
        document.Id = docId + 100; // Mismatch

        var result = await _sut.Put(docId, document);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        Assert.IsType<ProblemDetails>(obj.Value);
    }

    // ────────────────────────── Delete ──────────────────────────────

    [Fact]
    public async Task Delete_RemovesDocument()
    {
        var docId = SeedDocument();

        var result = await _sut.Delete(docId);

        Assert.IsType<NoContentResult>(result);

        using var verifyCtx = CreateSeedContext();
        Assert.Empty(await verifyCtx.Documents.ToListAsync());
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(999);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, obj.StatusCode);
        Assert.IsType<ProblemDetails>(obj.Value);
    }

    [Fact]
    public async Task Delete_DoesNotAffectOtherDocuments()
    {
        var docId1 = SeedDocument();
        var docId2 = SeedDocument();

        var result = await _sut.Delete(docId1);

        Assert.IsType<NoContentResult>(result);

        using var verifyCtx = CreateSeedContext();
        var remaining = await verifyCtx.Documents.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(docId2, remaining[0].Id);
    }
}
