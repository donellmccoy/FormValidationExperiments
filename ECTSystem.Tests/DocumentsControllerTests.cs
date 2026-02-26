using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class DocumentsControllerTests : ControllerTestBase
{
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly Mock<IApiLogService> _mockLog;
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

        _mockLog = new Mock<IApiLogService>();

        _sut = new DocumentsController(_mockContextFactory.Object, _mockLog.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    private async Task SeedDocumentAsync(LineOfDutyDocument doc)
    {
        await using var context = new EctDbContext(_dbOptions);
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
    }

    // ─────────────────────────── GetByCaseId ─────────────────────────────────

    [Fact]
    public async Task GetByCaseId_ReturnsOkWithDocumentList()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "a.pdf", ContentType = "application/pdf" });
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 2, LineOfDutyCaseId = 1, FileName = "b.pdf", ContentType = "application/pdf" });

        var result = await _sut.GetByCaseId(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var docs = Assert.IsType<List<LineOfDutyDocument>>(ok.Value);
        Assert.Equal(2, docs.Count);
    }

    // ─────────────────────────────── GetById ─────────────────────────────────

    [Fact]
    public async Task GetById_WhenDocumentFound_ReturnsOkWithDocument()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "form.pdf", ContentType = "application/pdf" });

        var result = await _sut.GetById(caseId: 1, documentId: 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var doc = Assert.IsType<LineOfDutyDocument>(ok.Value);
        Assert.Equal("form.pdf", doc.FileName);
    }

    [Fact]
    public async Task GetById_WhenDocumentNotFound_ReturnsNotFound()
    {
        var result = await _sut.GetById(caseId: 1, documentId: 99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetById_WhenDocumentBelongsToDifferentCase_ReturnsNotFound()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 2, FileName = "other.pdf", ContentType = "application/pdf" });

        var result = await _sut.GetById(caseId: 1, documentId: 1);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Download ────────────────────────────────

    [Fact]
    public async Task Download_WhenDocumentAndContentExist_ReturnsFileResult()
    {
        var content = new byte[] { 1, 2, 3, 4 };
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "af348.pdf", ContentType = "application/pdf", Content = content });

        var result = await _sut.Download(caseId: 1, documentId: 1);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("af348.pdf",       file.FileDownloadName);
        Assert.Equal(content,           file.FileContents);
    }

    [Fact]
    public async Task Download_WhenDocumentNotFound_ReturnsNotFound()
    {
        var result = await _sut.Download(caseId: 1, documentId: 99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Download_WhenContentIsEmpty_ReturnsFileResult()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "af348.pdf", ContentType = "application/pdf", Content = Array.Empty<byte>() });

        var result = await _sut.Download(caseId: 1, documentId: 1);

        // Content is empty byte[] (not null) so it returns a file result
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Empty(file.FileContents);
    }

    // ─────────────────────────────── Upload ──────────────────────────────────

    [Fact]
    public async Task Upload_WhenFileIsNull_ReturnsBadRequest()
    {
        var result = await _sut.Upload(1, null, "AF Form 348");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_WhenFileIsEmpty_ReturnsBadRequest()
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await _sut.Upload(1, mockFile.Object, "AF Form 348");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_WhenFileIsValid_ReturnsCreated()
    {
        var bytes    = new byte[256];
        var mockFile = BuildMockFile("upload.pdf", "application/pdf", bytes);

        var result = await _sut.Upload(1, mockFile.Object, "AF Form 348");

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var doc = Assert.IsType<LineOfDutyDocument>(created.Value);
        Assert.Equal("upload.pdf", doc.FileName);
        Assert.Equal(1, doc.LineOfDutyCaseId);
    }

    // ─────────────────────────────── Delete ──────────────────────────────────

    [Fact]
    public async Task Delete_WhenDocumentFound_ReturnsNoContent()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "del.pdf", ContentType = "application/pdf" });

        var result = await _sut.Delete(caseId: 1, documentId: 1);

        Assert.IsType<NoContentResult>(result);

        // Verify document was actually removed
        await using var context = new EctDbContext(_dbOptions);
        Assert.Null(await context.Documents.FindAsync(1));
    }

    [Fact]
    public async Task Delete_WhenDocumentNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(caseId: 1, documentId: 99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenDocumentBelongsToDifferentCase_ReturnsNotFound()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 2, FileName = "other.pdf", ContentType = "application/pdf" });

        var result = await _sut.Delete(caseId: 1, documentId: 1);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Helpers ─────────────────────────────────

    private static Mock<IFormFile> BuildMockFile(string fileName, string contentType, byte[] content)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(content));
        return mock;
    }
}
