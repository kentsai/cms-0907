using CMS.API.Infrastructure;

namespace CMS.API.Models;

/// <summary>
/// Response model for <c>dbo.FeaturedPromoItem</c>: one promotion placed in a slot of a training centre's home
/// page on a given day. <see cref="PromoCode"/> and <see cref="TrainingCenterName"/> are JOINed in every SELECT.
/// </summary>
public class FeaturedPromoItem
{
    public const byte MinSlot = 1;
    public const byte MaxSlot = 3;

    public int Pkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public short TrainingCenterPkid { get; set; }
    public byte Slot { get; set; }
    public int PromotionPkid { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary><c>TrainingCenter.Name</c> (INNER JOIN).</summary>
    [AuditIgnore]
    public string TrainingCenterName { get; set; } = string.Empty;

    /// <summary><c>Promotion2.PromoCode</c> (INNER JOIN).</summary>
    [AuditIgnore]
    public string PromoCode { get; set; } = string.Empty;
}
