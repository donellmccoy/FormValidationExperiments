using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
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

    // ──────────────────────────── Get (collection) ───────────────────────────

    [Fact]
    public void Get_ReturnsOkWithDocumentsQueryable()
    {
        var result = _sut.Get();

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

    // ─────────────────────── GetMediaResource ($value) ───────────────────────

    [Fact]
    public async Task GetMediaResource_WhenDocumentAndContentExist_ReturnsFileResult()
    {
        var content = new byte[] { 1, 2, 3, 4 };
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "af348.pdf", ContentType = "application/pdf", Content = content });

        var result = await _sut.GetMediaResource(key: 1);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("af348.pdf",       file.FileDownloadName);
        Assert.Equal(content,           file.FileContents);
    }

    [Fact]
    public async Task GetMediaResource_WhenDocumentNotFound_ReturnsNotFound()
    {
        var result = await _sut.GetMediaResource(key: 99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetMediaResource_WhenContentIsEmpty_ReturnsNotFound()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "af348.pdf", ContentType = "application/pdf", Content = Array.Empty<byte>() });

        var result = await _sut.GetMediaResource(key: 1);

        Assert.IsType<NotFoundResult>(result);
    }

    // ──────────────────────────────── Upload ─────────────────────────────────

    [Fact]
    public async Task Upload_WhenFileIsNull_ReturnsBadRequest()
    {
        var result = await _sut.Upload(caseId: 1, file: null, documentType: "AF Form 348");

        Assert.IsType<BadRequestODataResult>(result);
    }

    [Fact]
    public async Task Upload_WhenFileIsEmpty_ReturnsBadRequest()
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await _sut.Upload(caseId: 1, file: mockFile.Object, documentType: "AF Form 348");

        Assert.IsType<BadRequestODataResult>(result);
    }

    [Fact]
    public async Task Upload_WhenFileIsValid_ReturnsCreated()
    {
        var bytes    = new byte[256];
        var mockFile = BuildMockFile("upload.pdf", "application/pdf", bytes);

        var result = await _sut.Upload(caseId: 1, file: mockFile.Object, documentType: "AF Form 348");

        var created = Assert.IsType<CreatedODataResult<LineOfDutyDocument>>(result);
        var doc = (LineOfDutyDocument)created.Value;
        Assert.Equal("upload.pdf", doc.FileName);
        Assert.Equal(1, doc.LineOfDutyCaseId);
    }

    // ──────────────────────────────── Delete ─────────────────────────────────

    [Fact]
    public async Task Delete_WhenDocumentFound_ReturnsNoContent()
    {
        await SeedDocumentAsync(new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "del.pdf", ContentType = "application/pdf" });

        var result = await _sut.Delete(key: 1);

        Assert.IsType<NoContentResult>(result);

        // Verify document was actually removed
        await using var context = new EctDbContext(_dbOptions);
        Assert.Null(await context.Documents.FindAsync(1));
    }

    [Fact]
    public async Task Delete_WhenDocumentNotFound_ReturnsNotFound()
    {
        var result = await _sut.Delete(key: 99);

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
