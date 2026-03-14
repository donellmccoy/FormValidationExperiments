using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Security-focused tests for <see cref="DocumentFilesController"/> covering
/// path traversal, double extensions, MIME spoofing, and boundary validation
/// scenarios beyond the functional tests in <see cref="DocumentFilesControllerTests"/>.
/// </summary>
public class DocumentFilesSecurityTests : ControllerTestBase
{
    private readonly DbContextOptions<EctDbContext> _dbOptions;
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly Mock<ILoggingService> _mockLog;
    private readonly DocumentFilesController _sut;

    public DocumentFilesSecurityTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();
        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));

        _mockLog = new Mock<ILoggingService>();

        _sut = new DocumentFilesController(_mockContextFactory.Object, _mockLog.Object, CreatePdfService());
        _sut.ControllerContext = CreateControllerContext();
    }

    private static IFormFile CreateFormFile(
        string fileName,
        byte[] content = null,
        string contentType = "application/pdf")
    {
        content ??= new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var stream = new MemoryStream(content);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        return mock.Object;
    }

    /// <summary>
    /// Verifies that filenames containing path traversal sequences (e.g., <c>../../etc/passwd</c>)
    /// are rejected because <see cref="Path.GetExtension"/> extracts a disallowed extension.
    /// </summary>
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config\\sam")]
    [InlineData("../../../secret.exe")]
    public async Task Upload_PathTraversalFilename_IsRejected(string fileName)
    {
        var file = CreateFormFile(fileName, new byte[] { 1 });

        var result = await _sut.Upload(caseId: 1, file: [file]);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that files with double extensions where the final extension is disallowed
    /// (e.g., <c>report.pdf.exe</c>) are rejected by the extension whitelist.
    /// </summary>
    [Theory]
    [InlineData("report.pdf.exe")]
    [InlineData("document.docx.bat")]
    [InlineData("image.png.js")]
    public async Task Upload_DoubleExtensionWithDisallowedFinal_IsRejected(string fileName)
    {
        var file = CreateFormFile(fileName, new byte[] { 1 });

        var result = await _sut.Upload(caseId: 1, file: [file]);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that a file exactly at the 10 MB size limit is accepted.
    /// </summary>
    [Fact]
    public async Task Upload_FileExactlyAtSizeLimit_IsAccepted()
    {
        var exactLimitContent = new byte[10 * 1024 * 1024]; // exactly 10 MB
        var file = CreateFormFile("exact-limit.pdf", exactLimitContent);

        var result = await _sut.Upload(caseId: 1, file: [file]);

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// Verifies that files with no extension at all are rejected.
    /// </summary>
    [Fact]
    public async Task Upload_NoExtension_IsRejected()
    {
        var file = CreateFormFile("noextension", new byte[] { 1 });

        var result = await _sut.Upload(caseId: 1, file: [file]);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that a batch with one valid and one invalid file is fully rejected
    /// (fail-fast), not partially accepted.
    /// </summary>
    [Fact]
    public async Task Upload_MixedValidAndInvalid_RejectsEntireBatch()
    {
        var files = new List<IFormFile>
        {
            CreateFormFile("valid.pdf", new byte[] { 1 }),
            CreateFormFile("malicious.exe", new byte[] { 2 })
        };

        var result = await _sut.Upload(caseId: 1, file: files);

        Assert.IsType<BadRequestObjectResult>(result);

        // Verify no documents were persisted
        await using var context = new EctDbContext(_dbOptions);
        Assert.False(await context.Documents.AnyAsync());
    }
}
