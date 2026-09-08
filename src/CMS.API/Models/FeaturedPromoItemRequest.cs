using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for create/update. <see cref="Pkid"/> is ignored on create (IDENTITY) and identifies the row on
/// update. <see cref="PromotionPkid"/> comes from the PromoCode lookup on the form.
/// </summary>
public class FeaturedPromoItemRequest
{
    public int Pkid { get; set; }

    public DateOnly ScheduleOn { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "請選擇訓練中心。")]
    public short TrainingCenterPkid { get; set; }

    [Range(FeaturedPromoItem.MinSlot, FeaturedPromoItem.MaxSlot, ErrorMessage = "版位必須介於 1 到 3。")]
    public byte Slot { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "請輸入有效的促銷代碼。")]
    public int PromotionPkid { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Topic { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(300)]
    public string Description { get; set; } = string.Empty;
}
