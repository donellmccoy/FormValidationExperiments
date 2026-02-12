using Microsoft.AspNetCore.Mvc;
using FormValidationExperiments.Api.Mapping;
using FormValidationExperiments.Api.Services;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;

namespace FormValidationExperiments.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CasesController : ControllerBase
{
    private readonly ILineOfDutyCaseService _caseService;
    private readonly ILogger<CasesController> _logger;

    public CasesController(ILineOfDutyCaseService caseService, ILogger<CasesController> logger)
    {
        _caseService = caseService;
        _logger = logger;
    }

    /// <summary>
    /// Returns a paged result of LOD cases with optional filtering and sorting.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<LineOfDutyCase>>> GetPaged(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? filter = null,
        [FromQuery] string? orderBy = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _caseService.GetCasesPagedAsync(skip, take, filter, orderBy, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid filter/orderBy expression. Filter: {Filter}, OrderBy: {OrderBy}", filter, orderBy);
            return BadRequest(ex.Message);
        }
        catch (System.Linq.Dynamic.Core.Exceptions.ParseException ex)
        {
            _logger.LogWarning(ex, "Dynamic LINQ parse error. Filter: {Filter}, OrderBy: {OrderBy}", filter, orderBy);
            return BadRequest($"Invalid filter expression: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns a single LOD case with all navigation properties loaded.
    /// </summary>
    [HttpGet("{caseId}")]
    public async Task<ActionResult<LineOfDutyCase>> GetByCaseId(string caseId, CancellationToken ct = default)
    {
        var lodCase = await _caseService.GetCaseByCaseIdAsync(caseId, ct);
        if (lodCase is null)
            return NotFound();

        return Ok(lodCase);
    }

    /// <summary>
    /// Returns mapped view models for a specific case.
    /// </summary>
    [HttpGet("{caseId}/viewmodels")]
    public async Task<ActionResult<CaseViewModelsDto>> GetViewModels(string caseId, CancellationToken ct = default)
    {
        var lodCase = await _caseService.GetCaseByCaseIdAsync(caseId, ct);
        if (lodCase is null)
            return NotFound();

        var dto = new CaseViewModelsDto
        {
            CaseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(lodCase),
            MemberInfo = LineOfDutyCaseMapper.ToMemberInfoFormModel(lodCase),
            MedicalAssessment = LineOfDutyCaseMapper.ToMedicalAssessmentFormModel(lodCase),
            CommanderReview = LineOfDutyCaseMapper.ToCommanderReviewFormModel(lodCase),
            LegalSJAReview = LineOfDutyCaseMapper.ToLegalSJAReviewFormModel(lodCase)
        };

        return Ok(dto);
    }

    /// <summary>
    /// Saves all view model changes for a case.
    /// Applies reverse mapping and persists to the database.
    /// </summary>
    [HttpPut("{caseId}")]
    public async Task<ActionResult> SaveCase(string caseId, [FromBody] CaseViewModelsDto dto, CancellationToken ct = default)
    {
        var lodCase = await _caseService.UpdateCaseAsync(caseId, entity =>
        {
            LineOfDutyCaseMapper.ApplyMemberInfo(dto.MemberInfo, entity);
            LineOfDutyCaseMapper.ApplyMedicalAssessment(dto.MedicalAssessment, entity);
            LineOfDutyCaseMapper.ApplyCommanderReview(dto.CommanderReview, entity);
            LineOfDutyCaseMapper.ApplyLegalSJAReview(dto.LegalSJAReview, entity);
        }, ct);

        if (lodCase is null)
            return NotFound();

        var updatedInfo = LineOfDutyCaseMapper.ToCaseInfoModel(lodCase);
        return Ok(updatedInfo);
    }
}
