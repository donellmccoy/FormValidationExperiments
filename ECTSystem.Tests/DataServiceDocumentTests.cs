using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;
using ECTSystem.Tests.TestData;
using Xunit;

namespace ECTSystem.Tests;

public class DataServiceDocumentTests : DataServiceTestBase
{
    // ──────────────────────── GetDocumentsByCaseIdAsync ────────────────────────

    [Fact]
    public async Task GetDocumentsByCaseIdAsync_WhenDocumentsExist_ReturnsDocumentsForCase()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        ctx.Documents.AddRange(
            new LineOfDutyDocument { LineOfDutyCaseId = 1, FileName = "a.pdf", ContentType = "application/pdf", Content = [1, 2, 3] },
            new LineOfDutyDocument { LineOfDutyCaseId = 1, FileName = "b.pdf", ContentType = "application/pdf", Content = [4, 5, 6] },
            new LineOfDutyDocument { LineOfDutyCaseId = 2, FileName = "c.pdf", ContentType = "application/pdf", Content = [7, 8, 9] }
        );
        await ctx.SaveChangesAsync();

        var result = await Sut.GetDocumentsByCaseIdAsync(1);

        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Equal(1, d.LineOfDutyCaseId));
    }

    [Fact]
    public async Task GetDocumentsByCaseIdAsync_DoesNotReturnFileContent()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        ctx.Documents.Add(new LineOfDutyDocument
        {
            LineOfDutyCaseId = 1,
            FileName    = "secure.pdf",
            ContentType = "application/pdf",
            Content     = [0xDE, 0xAD, 0xBE, 0xEF]
        });
        await ctx.SaveChangesAsync();

        var result = await Sut.GetDocumentsByCaseIdAsync(1);

        Assert.Single(result);
        // Content should NOT be returned by this method (defaults to empty array)
        Assert.Empty(result[0].Content);
    }

    [Fact]
    public async Task GetDocumentsByCaseIdAsync_WhenNoDocumentsForCase_ReturnsEmptyList()
    {
        var result = await Sut.GetDocumentsByCaseIdAsync(1);

        Assert.Empty(result);
    }

    // ──────────────────────────── GetDocumentByIdAsync ────────────────────────

    [Theory]
    [ClassData(typeof(DocumentKeyExistsTestData))]
    public async Task GetDocumentByIdAsync_ReturnsExpectedResult(int documentId, bool seedDocument)
    {
        if (seedDocument)
        {
            await using var ctx = CreateSeedContext();
            ctx.Cases.Add(BuildCase(1));
            await ctx.SaveChangesAsync();
            ctx.Documents.Add(new LineOfDutyDocument
            {
                Id              = documentId,
                LineOfDutyCaseId = 1,
                FileName        = "test.pdf",
                ContentType     = "application/pdf",
                Content         = [1, 2, 3]
            });
            await ctx.SaveChangesAsync();
        }

        var result = await Sut.GetDocumentByIdAsync(documentId);

        if (seedDocument)
        {
            Assert.NotNull(result);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task GetDocumentByIdAsync_WhenDocumentExists_DoesNotReturnContent()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();
        ctx.Documents.Add(new LineOfDutyDocument
        {
            LineOfDutyCaseId = 1,
            FileName         = "test.pdf",
            ContentType      = "application/pdf",
            Content          = [0xCA, 0xFE]
        });
        await ctx.SaveChangesAsync();

        var allDocs = await ctx.Documents.ToListAsync();
        var doc = allDocs.First();

        var result = await Sut.GetDocumentByIdAsync(doc.Id);

        Assert.NotNull(result);
        Assert.Empty(result.Content);
    }

    // ──────────────────────── GetDocumentContentAsync ────────────────────────

    [Fact]
    public async Task GetDocumentContentAsync_WhenDocumentExists_ReturnsBytes()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var expectedContent = new byte[] { 1, 2, 3, 4, 5 };
        ctx.Documents.Add(new LineOfDutyDocument
        {
            LineOfDutyCaseId = 1,
            FileName         = "test.pdf",
            ContentType      = "application/pdf",
            Content          = expectedContent
        });
        await ctx.SaveChangesAsync();

        var allDocs = await ctx.Documents.ToListAsync();
        var docId = allDocs.First().Id;

        var result = await Sut.GetDocumentContentAsync(docId);

        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public async Task GetDocumentContentAsync_WhenDocumentNotFound_ReturnsNull()
    {
        var result = await Sut.GetDocumentContentAsync(999);

        Assert.Null(result);
    }

    // ──────────────────────────── UploadDocumentAsync ────────────────────────

    [Theory]
    [ClassData(typeof(DocumentUploadSizeTestData))]
    public async Task UploadDocumentAsync_RespectsMaxFileSizeLimit(int fileSizeBytes, bool shouldSucceed)
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var bytes = new byte[fileSizeBytes];
        using var stream = new MemoryStream(bytes);

        if (shouldSucceed)
        {
            var result = await Sut.UploadDocumentAsync(
                caseId:       1,
                fileName:     "upload.pdf",
                contentType:  "application/pdf",
                documentType: "AF Form 348",
                description:  "Test upload",
                content:      stream);

            Assert.NotNull(result);
            Assert.Equal(fileSizeBytes, result.FileSize);
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                Sut.UploadDocumentAsync(1, "upload.pdf", "application/pdf", "AF Form 348", "Test", stream));
        }
    }

    [Fact]
    public async Task UploadDocumentAsync_WhenValid_DoesNotReturnContentInResponse()
    {
        await using var ctx = CreateSeedContext();
        ctx.Cases.Add(BuildCase(1));
        await ctx.SaveChangesAsync();

        var bytes = new byte[512];
        using var stream = new MemoryStream(bytes);

        var result = await Sut.UploadDocumentAsync(
            caseId:       1,
            fileName:     "form.pdf",
            contentType:  "application/pdf",
            documentType: "AF Form 348",
            description:  "Unit test document",
            content:      stream);

        Assert.Null(result.Content);
    }

    [Fact]
    public async Task UploadDocumentAsync_WhenValid_PersistsDocumentToDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var bytes = new byte[256];
        using var stream = new MemoryStream(bytes);

        await Sut.UploadDocumentAsync(1, "report.pdf", "application/pdf", "Medical Report", "Desc", stream);

        await using var verifyCtx = CreateSeedContext();
        var saved = await verifyCtx.Documents
            .Where(d => d.LineOfDutyCaseId == 1)
            .FirstOrDefaultAsync();

        Assert.NotNull(saved);
        Assert.Equal("report.pdf", saved.FileName);
        Assert.Equal(256, saved.FileSize);
    }

    [Fact]
    public async Task UploadDocumentAsync_WhenValid_SetsUploadDateToUtcNow()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();

        var before = DateTime.UtcNow;
        using var stream = new MemoryStream(new byte[64]);

        var result = await Sut.UploadDocumentAsync(1, "x.pdf", "application/pdf", "DocType", "Desc", stream);

        Assert.NotNull(result.UploadDate);
        Assert.True(result.UploadDate >= before);
        Assert.True(result.UploadDate <= DateTime.UtcNow);
    }

    // ──────────────────────────── DeleteDocumentAsync ────────────────────────

    [Theory]
    [ClassData(typeof(DocumentKeyExistsTestData))]
    public async Task DeleteDocumentAsync_ReturnsExpectedResult(int documentId, bool seedDocument)
    {
        if (seedDocument)
        {
            await using var ctx = CreateSeedContext();
            ctx.Cases.Add(BuildCase(1));
            await ctx.SaveChangesAsync();
            ctx.Documents.Add(new LineOfDutyDocument
            {
                Id               = documentId,
                LineOfDutyCaseId = 1,
                FileName         = "del.pdf",
                ContentType      = "application/pdf",
                Content          = [0]
            });
            await ctx.SaveChangesAsync();
        }

        var result = await Sut.DeleteDocumentAsync(documentId);

        Assert.Equal(seedDocument, result);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WhenDocumentExists_RemovesFromDatabase()
    {
        await using var seedCtx = CreateSeedContext();
        seedCtx.Cases.Add(BuildCase(1));
        await seedCtx.SaveChangesAsync();
        seedCtx.Documents.Add(new LineOfDutyDocument
        {
            LineOfDutyCaseId = 1,
            FileName         = "remove.pdf",
            ContentType      = "application/pdf",
            Content          = [9, 8, 7]
        });
        await seedCtx.SaveChangesAsync();

        var allDocs = await seedCtx.Documents.ToListAsync();
        var docId = allDocs.First().Id;

        await Sut.DeleteDocumentAsync(docId);

        await using var verifyCtx = CreateSeedContext();
        Assert.False(await verifyCtx.Documents.AnyAsync(d => d.Id == docId));
    }
}
