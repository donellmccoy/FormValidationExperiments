using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class DocumentsControllerTests : ControllerTestBase
{
    private readonly Mock<ILineOfDutyDocumentService> _mockDocumentService;
    private readonly Mock<IApiLogService>             _mockLog;
    private readonly DocumentsController             _sut;

    public DocumentsControllerTests()
    {
        _mockDocumentService = new Mock<ILineOfDutyDocumentService>();
        _mockLog             = new Mock<IApiLogService>();

        _sut = new DocumentsController(_mockDocumentService.Object, _mockLog.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────── GetByCaseId ─────────────────────────────────

    [Fact]
    public async Task GetByCaseId_ReturnsOkWithDocumentList()
    {
        var documents = new List<LineOfDutyDocument>
        {
            new() { Id = 1, LineOfDutyCaseId = 1, FileName = "a.pdf" },
            new() { Id = 2, LineOfDutyCaseId = 1, FileName = "b.pdf" }
        };
        _mockDocumentService.Setup(s => s.GetDocumentsByCaseIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var result = await _sut.GetByCaseId(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(documents, ok.Value);
    }

    // ─────────────────────────────── GetById ─────────────────────────────────

    [Fact]
    public async Task GetById_WhenDocumentFound_ReturnsOkWithDocument()
    {
        var document = new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "form.pdf", ContentType = "application/pdf" };
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await _sut.GetById(caseId: 1, documentId: 1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(document, ok.Value);
    }

    [Fact]
    public async Task GetById_WhenDocumentNotFound_ReturnsNotFound()
    {
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LineOfDutyDocument)null);

        var result = await _sut.GetById(caseId: 1, documentId: 99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetById_WhenDocumentBelongsToDifferentCase_ReturnsNotFound()
    {
        // Document belongs to case 2, but request targets case 1
        var document = new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 2, FileName = "other.pdf" };
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await _sut.GetById(caseId: 1, documentId: 1);

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────── Download ────────────────────────────────

    [Fact]
    public async Task Download_WhenDocumentAndContentExist_ReturnsFileResult()
    {
        var document = new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "af348.pdf", ContentType = "application/pdf" };
        var content  = new byte[] { 1, 2, 3, 4 };
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        _mockDocumentService.Setup(s => s.GetDocumentContentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var result = await _sut.Download(caseId: 1, documentId: 1);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("af348.pdf",       file.FileDownloadName);
        Assert.Equal(content,           file.FileContents);
    }

    [Fact]
    public async Task Download_WhenDocumentNotFound_ReturnsNotFound()
    {
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LineOfDutyDocument)null);

        var result = await _sut.Download(caseId: 1, documentId: 99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Download_WhenContentNotFound_ReturnsNotFound()
    {
        var document = new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "af348.pdf", ContentType = "application/pdf" };
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        _mockDocumentService.Setup(s => s.GetDocumentContentAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null);

        var result = await _sut.Download(caseId: 1, documentId: 1);

        Assert.IsType<NotFoundResult>(result);
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
        var saved    = new LineOfDutyDocument { Id = 7, LineOfDutyCaseId = 1, FileName = "upload.pdf", ContentType = "application/pdf" };

        _mockDocumentService.Setup(s => s.UploadDocumentAsync(
                1, "upload.pdf", "application/pdf", "AF Form 348", "",
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);

        var result = await _sut.Upload(1, mockFile.Object, "AF Form 348");

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Upload_WhenServiceThrowsArgumentException_ReturnsBadRequest()
    {
        var mockFile = BuildMockFile("big.pdf", "application/pdf", new byte[1024]);

        _mockDocumentService.Setup(s => s.UploadDocumentAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("File size exceeds the maximum allowed size of 10 MB."));

        var result = await _sut.Upload(1, mockFile.Object, "AF Form 348");

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("File size exceeds the maximum allowed size of 10 MB.", bad.Value);
    }

    // ─────────────────────────────── Delete ──────────────────────────────────

    [Fact]
    public async Task Delete_WhenDocumentFound_ReturnsNoContent()
    {
        var document = new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 1, FileName = "del.pdf" };
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await _sut.Delete(caseId: 1, documentId: 1);

        Assert.IsType<NoContentResult>(result);
        _mockDocumentService.Verify(s => s.DeleteDocumentAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenDocumentNotFound_ReturnsNotFound()
    {
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LineOfDutyDocument)null);

        var result = await _sut.Delete(caseId: 1, documentId: 99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenDocumentBelongsToDifferentCase_ReturnsNotFound()
    {
        var document = new LineOfDutyDocument { Id = 1, LineOfDutyCaseId = 2, FileName = "other.pdf" };
        _mockDocumentService.Setup(s => s.GetDocumentByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

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
