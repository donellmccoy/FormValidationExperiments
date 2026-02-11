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

    public CasesController(ILineOfDutyCaseService caseService)
    {
        _caseService = caseService;
    }

    /// <summary>
    /// Returns a paged result of LOD cases with optional filtering and sorting.
    /// </summary>
    [HttpGet("paged")]
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
            return BadRequest(ex.Message);
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
