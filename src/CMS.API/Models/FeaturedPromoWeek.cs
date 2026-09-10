namespace CMS.API.Models;

/// <summary>
/// Response of <c>GET /api/featured-promo-items/week</c>: every item of one training centre within a
/// Monday-to-Sunday week, plus the resolved week bounds so the client can render the 7-day board.
/// </summary>
public class FeaturedPromoWeek
{
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public short TrainingCenterPkid { get; set; }
    public IReadOnlyList<FeaturedPromoItem> Items { get; set; } = [];
}
