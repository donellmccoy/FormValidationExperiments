using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Api.Data;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.Api.Controllers;

/// <summary>
/// OData-enabled controller for querying LOD cases.
/// The Radzen DataGrid sends OData-compatible $filter, $orderby, $top, $skip, $count
/// query parameters which the OData middleware translates directly into EF Core LINQ queries.
/// Named "CasesController" to match the OData entity set "Cases" (convention routing).
/// </summary>
public class CasesController : ODataController
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public CasesController(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        // Create a long-lived context — OData needs the query to remain open
        // until the response is serialized. The context will be disposed by the DI scope.
        var context = _contextFactory.CreateDbContext();
        return Ok(context.Cases.AsNoTracking());
    }
}
