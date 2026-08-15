using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/admin/backfill")]
public sealed class AdminBackfillController(IPaymentOrderBackfillService backfillService) : ControllerBase
{
    /// <summary>
    /// Intentionally executes a controlled one-time backfill of existing PAID payments without orders.
    /// Supports dry-run mode (dryRun = true by default) to inspect eligible and skipped payments without modifying database.
    /// </summary>
    [HttpPost("orders")]
    [AllowAnonymous] // Intentionally open for explicit admin/backfill execution
    [ProducesResponseType(typeof(PaymentOrderBackfillResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentOrderBackfillResult>> ExecuteOrderBackfill(
        [FromQuery] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var result = await backfillService.ExecuteBackfillAsync(dryRun, cancellationToken);
        return Ok(result);
    }
}
