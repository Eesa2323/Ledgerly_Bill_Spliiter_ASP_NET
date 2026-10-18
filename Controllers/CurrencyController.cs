using BillSplitter.Contracts;
using BillSplitter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BillSplitter.Controllers;

[Authorize, ApiController, Route("api/currency")]
public sealed class CurrencyController(CurrencyService currency) : ControllerBase
{
    [HttpGet("convert")]
    public async Task<ActionResult<ConversionResponse>> Convert([FromQuery] ConversionRequest request, CancellationToken cancellationToken)
    {
    // The provider returns a decimal rate; rounding happens only for the displayed converted amount.
        if (request.Amount < 0 || request.From.Length != 3 || request.To.Length != 3) return BadRequest("Amount must be non-negative and currencies must be ISO 4217 codes.");
        var from = request.From.ToUpperInvariant();
        var to = request.To.ToUpperInvariant();
        var rate = await currency.GetRateAsync(from, to, cancellationToken);
        return Ok(new ConversionResponse(request.Amount, from, to, rate, decimal.Round(request.Amount * rate, 2, MidpointRounding.ToEven)));
    }
}
