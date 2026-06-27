using Asp.Versioning;
using iM1os.Application.Parts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/parts")]
public sealed class PartsController(IPartsEngineService partsEngineService) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyCollection<PartSearchResult>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 25, CancellationToken cancellationToken = default)
    {
        var results = await partsEngineService.SearchAsync(query, limit, cancellationToken);
        return Ok(results);
    }

    [HttpGet("{manufacturerPartId:guid}")]
    [ProducesResponseType<PartDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid manufacturerPartId, CancellationToken cancellationToken)
    {
        var detail = await partsEngineService.GetPartDetailAsync(manufacturerPartId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost]
    [ProducesResponseType<PartDetail>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateManufacturerPartRequest request, CancellationToken cancellationToken)
    {
        var detail = await partsEngineService.CreateManufacturerPartAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { manufacturerPartId = detail.Id, version = "1" }, detail);
    }

    [HttpPost("{manufacturerPartId:guid}/supplier-listings")]
    [ProducesResponseType<SupplierListingDetail>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddSupplierListing(Guid manufacturerPartId, AddSupplierListingRequest request, CancellationToken cancellationToken)
    {
        if (manufacturerPartId != request.ManufacturerPartId)
        {
            return BadRequest("Route manufacturer part id must match request manufacturer part id.");
        }

        var detail = await partsEngineService.AddSupplierListingAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { manufacturerPartId, version = "1" }, detail);
    }

    [HttpPut("{manufacturerPartId:guid}/inventory")]
    [ProducesResponseType<InventoryItemDetail>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetInventory(Guid manufacturerPartId, SetInventoryItemRequest request, CancellationToken cancellationToken)
    {
        if (manufacturerPartId != request.ManufacturerPartId)
        {
            return BadRequest("Route manufacturer part id must match request manufacturer part id.");
        }

        var detail = await partsEngineService.SetInventoryItemAsync(request, cancellationToken);
        return Ok(detail);
    }

    [HttpPut("{manufacturerPartId:guid}/supersession")]
    [ProducesResponseType<PartDetail>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Supersede(Guid manufacturerPartId, SupersedePartRequest request, CancellationToken cancellationToken)
    {
        var detail = await partsEngineService.SupersedePartAsync(manufacturerPartId, request, cancellationToken);
        return Ok(detail);
    }
}
