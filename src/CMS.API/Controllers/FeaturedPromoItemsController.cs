using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// Home-page promotion board. Besides the standard CRUD, <c>GET week</c> serves the one-week / one-centre board and
/// <c>move-up</c> / <c>move-down</c> reorder a row within its day.
/// </summary>
[ApiController]
[Route("api/featured-promo-items")]
[Produces("application/json")]
public class FeaturedPromoItemsController(IFeaturedPromoItemRepository repository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FeaturedPromoItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeaturedPromoItem>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await repository.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// All items of <paramref name="trainingCenterPkid"/> in the Monday-to-Sunday week that contains
    /// <paramref name="date"/> (today when omitted).
    /// </summary>
    [HttpGet("week")]
    [ProducesResponseType<FeaturedPromoWeek>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeaturedPromoWeek>> GetWeek(
        [FromQuery] short trainingCenterPkid, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        if (trainingCenterPkid <= 0)
        {
            return BadRequest(new { message = "請選擇訓練中心。" });
        }

        var week = WeekRange.For(date ?? DateOnly.FromDateTime(DateTime.Today));
        var items = await repository.GetWeekAsync(trainingCenterPkid, week.Start, week.End, cancellationToken);

        return Ok(new FeaturedPromoWeek
        {
            WeekStart = week.Start,
            WeekEnd = week.End,
            TrainingCenterPkid = trainingCenterPkid,
            Items = items
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<FeaturedPromoItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType<FeaturedPromoItem>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Create(
        [FromBody] FeaturedPromoItemRequest request, CancellationToken cancellationToken)
    {
        int pkid;
        try
        {
            pkid = await repository.CreateAsync(request, cancellationToken);
        }
        catch (SlotOccupiedException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        // Re-read so the response carries the JOINed PromoCode / centre name.
        var created = await repository.GetByIdAsync(pkid, cancellationToken) ?? new FeaturedPromoItem
        {
            Pkid = pkid,
            ScheduleOn = request.ScheduleOn,
            TrainingCenterPkid = request.TrainingCenterPkid,
            Slot = request.Slot,
            PromotionPkid = request.PromotionPkid,
            Topic = request.Topic,
            Description = request.Description
        };

        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] FeaturedPromoItemRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await repository.UpdateAsync(request, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (SlotOccupiedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Slot n → n-1 (swaps with the occupant, if any). 400 when already on slot 1.</summary>
    [HttpPost("{id:int}/move-up")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> MoveUp(int id, CancellationToken cancellationToken) => MoveAsync(id, -1, cancellationToken);

    /// <summary>Slot n → n+1 (swaps with the occupant, if any). 400 when already on slot 3.</summary>
    [HttpPost("{id:int}/move-down")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> MoveDown(int id, CancellationToken cancellationToken) => MoveAsync(id, +1, cancellationToken);

    private async Task<IActionResult> MoveAsync(int id, int delta, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var target = item.Slot + delta;
        if (target < FeaturedPromoItem.MinSlot || target > FeaturedPromoItem.MaxSlot)
        {
            return BadRequest(new { message = $"版位 {item.Slot} 無法再{(delta < 0 ? "上" : "下")}移。" });
        }

        var moved = await repository.SwapSlotAsync(id, (byte)target, cancellationToken);
        return moved ? NoContent() : NotFound();
    }
}
