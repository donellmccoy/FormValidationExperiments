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
/// Unit tests for <see cref="DocumentFilesController"/>, the REST controller that handles
/// binary file upload, download, and deletion for LOD case documents.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the OData-based <see cref="DocumentsController"/> (which serves metadata only),
/// <see cref="DocumentFilesController"/> manages the actual file bytes — downloading content
/// as file attachments, accepting multi-file uploads with size and extension validation, and
/// permanently deleting document records.
/// </para>
/// <para>
/// Each test instance creates an isolated in-memory EF Core database keyed by a unique GUID.
/// The controller under test receives a mocked <see cref="IDbContextFactory{EctDbContext}"/>
/// and <see cref="ILoggingService"/>.
/// </para>
/// </remarks>
public class DocumentFilesControllerTests : ControllerTestBase
{
    /// <summary>In-memory database options shared across seed, act, and verify phases.</summary>
    private readonly DbContextOptions<EctDbContext> _dbOptions;

    /// <summary>Mocked context factory returning <see cref="EctDbContext"/> instances backed by the in-memory store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;

    /// <summary>Mocked logging service injected into the controller.</summary>
    private readonly Mock<ILoggingService> _mockLog;

    /// <summary>System under test — the <see cref="DocumentFilesController"/> instance.</summary>
    private readonly DocumentFilesController _sut;

    /// <summary>
    /// Initializes the in-memory database, configures mocked dependencies, and creates
    /// the <see cref="DocumentFilesController"/> with a fake authenticated user context.
    /// </summary>
    public DocumentFilesControllerTests()
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

        _sut = new DocumentFilesController(_mockContextFactory.Object, _mockLog.Object, CreatePdfService());
        _sut.ControllerContext = CreateControllerContext();
    }

    /// <summary>
    /// Seeds a <see cref="LineOfDutyDocument"/> into the in-memory database for test arrangement.
    /// </summary>
    private async Task SeedDocumentAsync(LineOfDutyDocument doc)
    {
        await using var context = new EctDbContext(_dbOptions);
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a <see cref="LineOfDutyCase"/> (with its required <see cref="Member"/>) into the
    /// in-memory database so that foreign-key constraints are satisfied for document operations.
    /// </summary>
    private async Task SeedCaseAsync(int caseId = 1)
    {
        await using var context = new EctDbContext(_dbOptions);
        if (!await context.Members.AnyAsync(m => m.Id == 1))
        {
            context.Members.Add(new Member { Id = 1, FirstName = "John", LastName = "Doe" });
        }

        context.Cases.Add(BuildCase(id: caseId));
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a mock <see cref="IFormFile"/> with the specified file name, content, and MIME type.
    /// </summary>
    private static IFormFile CreateFormFile(
        string fileName = "test.pdf",
        byte[] content = null,
        string contentType = "application/pdf")
    {
        content ??= new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF header bytes
        var stream = new MemoryStream(content);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.ContentType).Returns(contentType);
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        return mock.Object;
    }

    // ════════════════════════════ Download Tests ════════════════════════════

    /// <summary>
    /// Verifies that downloading an existing document with valid content returns a
    /// <see cref="FileContentResult"/> with the correct content type and file name.
    /// </summary>
    [Fact]
    public async Task Download_ExistingDocument_ReturnsFileResult()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };
        await SeedDocumentAsync(new LineOfDutyDocument
        {
            Id = 10,
            LineOfDutyCaseId = 1,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            Content = content,
            FileSize = content.Length
        });

        var result = await _sut.Download(caseId: 1, key: 10);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("report.pdf", fileResult.FileDownloadName);
        Assert.Equal(content, fileResult.FileContents);
    }

    /// <summary>
    /// Verifies that requesting a non-existent document ID returns <see cref="NotFoundResult"/>.
    /// </summary>
    [Fact]
    public async Task Download_NonExistentDocument_ReturnsNotFound()
    {
        var result = await _sut.Download(caseId: 1, key: 999);

        Assert.IsType<NotFoundResult>(result);
        _mockLog.Verify(l => l.DocumentNotFound(999, 1), Times.Once);
    }

    /// <summary>
    /// Verifies that requesting a document that belongs to a different case returns
    /// <see cref="NotFoundResult"/> to prevent cross-case document access.
    /// </summary>
    [Fact]
    public async Task Download_DocumentBelongsToDifferentCase_ReturnsNotFound()
    {
        await SeedDocumentAsync(new LineOfDutyDocument
        {
            Id = 10,
            LineOfDutyCaseId = 2,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            Content = new byte[] { 1, 2, 3 },
            FileSize = 3
        });

        var result = await _sut.Download(caseId: 1, key: 10);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// Verifies that a document with null content returns <see cref="NotFoundResult"/>.
    /// </summary>
    [Fact]
    public async Task Download_NullContent_ReturnsNotFound()
    {
        await SeedDocumentAsync(new LineOfDutyDocument
        {
            Id = 10,
            LineOfDutyCaseId = 1,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            Content = null,
            FileSize = 0
        });

        var result = await _sut.Download(caseId: 1, key: 10);

        Assert.IsType<NotFoundResult>(result);
        _mockLog.Verify(l => l.DocumentContentNotFound(10), Times.Once);
    }

    /// <summary>
    /// Verifies that a document with an empty byte array for content returns <see cref="NotFoundResult"/>.
    /// </summary>
    [Fact]
    public async Task Download_EmptyContent_ReturnsNotFound()
    {
        await SeedDocumentAsync(new LineOfDutyDocument
        {
            Id = 10,
            LineOfDutyCaseId = 1,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            Content = Array.Empty<byte>(),
            FileSize = 0
        });

        var result = await _sut.Download(caseId: 1, key: 10);

        Assert.IsType<NotFoundResult>(result);
    }

    // ════════════════════════════ Upload Tests ════════════════════════════

    /// <summary>
    /// Verifies that uploading a single valid PDF file returns <see cref="OkObjectResult"/>
    /// containing a list with the persisted document metadata.
    /// </summary>
    [Fact]
    public async Task Upload_SingleValidFile_ReturnsOkWithDocuments()
    {
        var file = CreateFormFile("test.pdf", new byte[] { 1, 2, 3 });

        var result = await _sut.Upload(caseId: 1, file: [file]);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var documents = Assert.IsAssignableFrom<List<LineOfDutyDocument>>(okResult.Value);
        Assert.Single(documents);
        Assert.Equal("test.pdf", documents[0].FileName);
        Assert.Null(documents[0].Content); // Content is nulled out before response
    }

    /// <summary>
    /// Verifies that uploading multiple valid files returns all persisted documents.
    /// </summary>
    [Fact]
    public async Task Upload_MultipleValidFiles_ReturnsAllDocuments()
    {
        var files = new List<IFormFile>
        {
            CreateFormFile("doc1.pdf", new byte[] { 1, 2, 3 }),
            CreateFormFile("doc2.docx", new byte[] { 4, 5, 6 }, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
        };

        var result = await _sut.Upload(caseId: 1, file: files);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var documents = Assert.IsAssignableFrom<List<LineOfDutyDocument>>(okResult.Value);
        Assert.Equal(2, documents.Count);
    }

    /// <summary>
    /// Verifies that uploading a null file list returns <see cref="BadRequestObjectResult"/>.
    /// </summary>
    [Fact]
    public async Task Upload_NullFileList_ReturnsBadRequest()
    {
        var result = await _sut.Upload(caseId: 1, file: null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file provided.", badRequest.Value);
        _mockLog.Verify(l => l.InvalidUpload(1), Times.Once);
    }

    /// <summary>
    /// Verifies that uploading an empty file list returns <see cref="BadRequestObjectResult"/>.
    /// </summary>
    [Fact]
    public async Task Upload_EmptyFileList_ReturnsBadRequest()
    {
        var result = await _sut.Upload(caseId: 1, file: []);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file provided.", badRequest.Value);
    }

    /// <summary>
    /// Verifies that uploading a file with a disallowed extension (e.g., <c>.exe</c>)
    /// returns <see cref="BadRequestObjectResult"/> with an extension-specific error message.
    /// </summary>
    [Fact]
    public async Task Upload_DisallowedExtension_ReturnsBadRequest()
    {
        var file = CreateFormFile("malware.exe", new byte[] { 0x4D, 0x5A });

        var result = await _sut.Upload(caseId: 1, file: [file]);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(".exe", (string)badRequest.Value);
        _mockLog.Verify(l => l.InvalidUpload(1), Times.Once);
    }

    /// <summary>
    /// Verifies that uploading a file exceeding the 10 MB per-file limit returns
    /// <see cref="BadRequestObjectResult"/> with a size-specific error message.
    /// </summary>
    [Fact]
    public async Task Upload_FileTooLarge_ReturnsBadRequest()
    {
        var oversizedContent = new byte[10 * 1024 * 1024 + 1]; // 10 MB + 1 byte
        var file = CreateFormFile("large.pdf", oversizedContent);

        var result = await _sut.Upload(caseId: 1, file: [file]);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("exceeds the maximum", (string)badRequest.Value);
    }

    /// <summary>
    /// Verifies that the uploaded document is persisted to the database with the correct
    /// case association, file name, content type, and binary content.
    /// </summary>
    [Fact]
    public async Task Upload_ValidFile_PersistsToDatabase()
    {
        var content = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var file = CreateFormFile("persisted.pdf", content);

        await _sut.Upload(caseId: 42, file: [file], documentType: "Medical Record", description: "X-ray");

        await using var context = new EctDbContext(_dbOptions);
        var doc = await context.Documents.FirstOrDefaultAsync();
        Assert.NotNull(doc);
        Assert.Equal(42, doc.LineOfDutyCaseId);
        Assert.Equal("persisted.pdf", doc.FileName);
        Assert.Equal("Medical Record", doc.DocumentType);
        Assert.Equal("X-ray", doc.Description);
        Assert.Equal(content, doc.Content);
    }

    /// <summary>
    /// Verifies that all 14 allowed file extensions are accepted by the upload action.
    /// </summary>
    /// <param name="extension">The file extension to validate.</param>
    [Theory]
    [InlineData(".pdf")]
    [InlineData(".doc")]
    [InlineData(".docx")]
    [InlineData(".xls")]
    [InlineData(".xlsx")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".gif")]
    [InlineData(".tif")]
    [InlineData(".tiff")]
    [InlineData(".txt")]
    [InlineData(".rtf")]
    public async Task Upload_AllowedExtension_Succeeds(string extension)
    {
        var file = CreateFormFile($"file{extension}", new byte[] { 1 });

        var result = await _sut.Upload(caseId: 1, file: [file]);

        Assert.IsType<OkObjectResult>(result);
    }

    // ════════════════════════════ Delete Tests ════════════════════════════

    /// <summary>
    /// Verifies that deleting an existing document that belongs to the specified case
    /// returns <see cref="NoContentResult"/> and removes the record from the database.
    /// </summary>
    [Fact]
    public async Task Delete_ExistingDocument_ReturnsNoContentAndRemovesFromDb()
    {
        await SeedDocumentAsync(new LineOfDutyDocument
        {
            Id = 10,
            LineOfDutyCaseId = 1,
            FileName = "delete-me.pdf",
            ContentType = "application/pdf",
            Content = new byte[] { 1 },
            FileSize = 1
        });

        var result = await _sut.Delete(caseId: 1, key: 10);

        Assert.IsType<NoContentResult>(result);
        await using var context = new EctDbContext(_dbOptions);
        Assert.False(await context.Documents.AnyAsync(d => d.Id == 10));
        _mockLog.Verify(l => l.DocumentDeleted(10, 1), Times.Once);
    }

    /// <summary>
    /// Verifies that deleting a non-existent document returns <see cref="NotFoundResult"/>.
    /// </summary>
    [Fact]
    public async Task Delete_NonExistentDocument_ReturnsNotFound()
    {
        var result = await _sut.Delete(caseId: 1, key: 999);

        Assert.IsType<NotFoundResult>(result);
        _mockLog.Verify(l => l.DocumentNotFound(999, 1), Times.Once);
    }

    /// <summary>
    /// Verifies that deleting a document that belongs to a different case returns
    /// <see cref="NotFoundResult"/> to prevent cross-case deletion.
    /// </summary>
    [Fact]
    public async Task Delete_DocumentBelongsToDifferentCase_ReturnsNotFound()
    {
        await SeedDocumentAsync(new LineOfDutyDocument
        {
            Id = 10,
            LineOfDutyCaseId = 2,
            FileName = "other-case.pdf",
            ContentType = "application/pdf",
            Content = new byte[] { 1 },
            FileSize = 1
        });

        var result = await _sut.Delete(caseId: 1, key: 10);

        Assert.IsType<NotFoundResult>(result);

        // Verify the document was NOT deleted from the actual case
        await using var context = new EctDbContext(_dbOptions);
        Assert.True(await context.Documents.AnyAsync(d => d.Id == 10));
    }
}
