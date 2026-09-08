namespace CMS.API.Models;

/// <summary>
/// Result row of the PromoCode lookup (<c>GET /api/lookups/promotions?keyword=</c>). Carries the promotion's
/// own Topic / Description so a form can pre-fill them after the code is chosen.
/// </summary>
public class PromotionLookupItem
{
    public int Pkid { get; set; }
    public string PromoCode { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
