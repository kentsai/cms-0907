using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IFeaturedPromoItemRepository
{
    Task<IReadOnlyList<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Items of one training centre whose <c>ScheduleOn</c> falls in [<paramref name="weekStart"/>, <paramref name="weekEnd"/>], ordered by day then slot.</summary>
    Task<IReadOnlyList<FeaturedPromoItem>> GetWeekAsync(short trainingCenterPkid, DateOnly weekStart, DateOnly weekEnd, CancellationToken cancellationToken);

    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken);

    /// <summary>Inserts the row; returns the new IDENTITY pkid. Throws <see cref="Infrastructure.SlotOccupiedException"/> when the (day, centre, slot) is taken.</summary>
    Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matched. Throws <see cref="Infrastructure.SlotOccupiedException"/> when the (day, centre, slot) is taken.</summary>
    Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the row to <paramref name="targetSlot"/> on the same day / centre. If another row already occupies
    /// that slot the two swap. Returns false when the row does not exist.
    /// </summary>
    Task<bool> SwapSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken);
}
