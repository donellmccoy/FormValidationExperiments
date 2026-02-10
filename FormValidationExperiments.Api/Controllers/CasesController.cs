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
    /// Returns all LOD cases (lightweight — no navigation properties).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<LineOfDutyCase>>> GetAll()
    {
        var cases = await _caseService.GetAllCasesAsync();
        return Ok(cases);
    }

    /// <summary>
    /// Returns a single LOD case with all navigation properties loaded.
    /// </summary>
    [HttpGet("{caseId}")]
    public async Task<ActionResult<LineOfDutyCase>> GetByCaseId(string caseId)
    {
        var lodCase = await _caseService.GetCaseByCaseIdAsync(caseId);
        if (lodCase is null)
            return NotFound();

        return Ok(lodCase);
    }

    /// <summary>
    /// Returns mapped view models for a specific case.
    /// </summary>
    [HttpGet("{caseId}/viewmodels")]
    public async Task<ActionResult<CaseViewModelsDto>> GetViewModels(string caseId)
    {
        var lodCase = await _caseService.GetCaseByCaseIdAsync(caseId);
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
    public async Task<ActionResult> SaveCase(string caseId, [FromBody] CaseViewModelsDto dto)
    {
        var lodCase = await _caseService.GetCaseByCaseIdAsync(caseId);
        if (lodCase is null)
            return NotFound();

        // Reverse-map view models → domain entity
        LineOfDutyCaseMapper.ApplyMemberInfo(dto.MemberInfo, lodCase);
        LineOfDutyCaseMapper.ApplyMedicalAssessment(dto.MedicalAssessment, lodCase);
        LineOfDutyCaseMapper.ApplyCommanderReview(dto.CommanderReview, lodCase);
        LineOfDutyCaseMapper.ApplyLegalSJAReview(dto.LegalSJAReview, lodCase);

        await _caseService.UpdateCaseAsync(lodCase);

        // Return refreshed case info header
        var updatedInfo = LineOfDutyCaseMapper.ToCaseInfoModel(lodCase);
        return Ok(updatedInfo);
    }
}
