using System.ComponentModel.DataAnnotations;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class FeaturedPromoItemsControllerTests
{
    private readonly Mock<IFeaturedPromoItemRepository> _repository = new(MockBehavior.Strict);
    private readonly FeaturedPromoItemsController _controller;

    public FeaturedPromoItemsControllerTests()
    {
        _controller = new FeaturedPromoItemsController(_repository.Object);
    }

    private static FeaturedPromoItem Sample(int pkid = 1, byte slot = 1) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateOnly(2026, 3, 16),
        TrainingCenterPkid = 1,
        Slot = slot,
        PromotionPkid = 50,
        Topic = "成為能AI協作的程式設計師",
        Description = "轉職就業養成班，三大主流語言任你選",
        TrainingCenterName = "台北",
        PromoCode = "20251204_SkillTrainAI"
    };

    private static FeaturedPromoItemRequest SampleRequest(int pkid = 0) => new()
    {
        Pkid = pkid,
        ScheduleOn = new DateOnly(2026, 3, 16),
        TrainingCenterPkid = 1,
        Slot = 1,
        PromotionPkid = 50,
        Topic = "成為能AI協作的程式設計師",
        Description = "轉職就業養成班，三大主流語言任你選"
    };

    // ---- GET /api/featured-promo-items ----

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<FeaturedPromoItem> { Sample(1), Sample(2, 2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    // ---- GET /api/featured-promo-items/week (one-week ScheduleOn filter + TrainingCenter filter) ----

    [Theory]
    [InlineData(16)] // Monday
    [InlineData(18)] // Wednesday
    [InlineData(22)] // Sunday
    public async Task GetWeek_FiltersByTrainingCenter_AndSnapsAnyDateToMondayThroughSunday(int day)
    {
        var expected = new List<FeaturedPromoItem> { Sample(1) };
        _repository
            .Setup(r => r.GetWeekAsync(3, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 22), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetWeek(3, new DateOnly(2026, 3, day), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var week = Assert.IsType<FeaturedPromoWeek>(ok.Value);
        Assert.Equal(new DateOnly(2026, 3, 16), week.WeekStart);
        Assert.Equal(new DateOnly(2026, 3, 22), week.WeekEnd);
        Assert.Equal((short)3, week.TrainingCenterPkid);
        Assert.Same(expected, week.Items);
        _repository.Verify(r => r.GetWeekAsync(3, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 22), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWeek_DefaultsToTheCurrentWeek_WhenDateIsOmitted()
    {
        var expectedWeek = WeekRange.For(DateOnly.FromDateTime(DateTime.Today));
        _repository
            .Setup(r => r.GetWeekAsync(1, expectedWeek.Start, expectedWeek.End, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetWeek(1, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var week = Assert.IsType<FeaturedPromoWeek>(ok.Value);
        Assert.Equal(expectedWeek.Start, week.WeekStart);
        Assert.Equal(DayOfWeek.Monday, week.WeekStart.DayOfWeek);
        Assert.Equal(6, week.WeekEnd.DayNumber - week.WeekStart.DayNumber);
    }

    [Fact]
    public async Task GetWeek_ReturnsBadRequest_WhenTrainingCenterIsMissing()
    {
        var result = await _controller.GetWeek(0, new DateOnly(2026, 3, 16), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _repository.Verify(r => r.GetWeekAsync(It.IsAny<short>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- GET /api/featured-promo-items/{id} ----

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var item = Sample(3);
        _repository.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _controller.GetById(3, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(item, ok.Value);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((FeaturedPromoItem?)null);

        var result = await _controller.GetById(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- POST /api/featured-promo-items ----

    [Fact]
    public async Task Create_ReturnsCreatedAtGetById_WithReReadRow()
    {
        var request = SampleRequest();
        var stored = Sample(7);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(7);
        _repository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(FeaturedPromoItemsController.GetById), created.ActionName);
        Assert.Equal(7, created.RouteValues!["id"]);
        Assert.Same(stored, created.Value);
    }

    [Fact]
    public async Task Create_FallsBackToEchoingRequest_WhenReReadReturnsNull()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(8);
        _repository.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync((FeaturedPromoItem?)null);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var body = Assert.IsType<FeaturedPromoItem>(created.Value);
        Assert.Equal(8, body.Pkid);
        Assert.Equal(new DateOnly(2026, 3, 16), body.ScheduleOn);
        Assert.Equal((byte)1, body.Slot);
        Assert.Equal(50, body.PromotionPkid);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenSlotIsOccupied()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SlotOccupiedException("occupied"));

        var result = await _controller.Create(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    // ---- PUT /api/featured-promo-items ----

    [Fact]
    public async Task Update_ReturnsNoContent_WhenRowUpdated()
    {
        var request = SampleRequest(1);
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenNoRowMatched()
    {
        var request = SampleRequest(42);
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenSlotIsOccupied()
    {
        var request = SampleRequest(1);
        _repository.Setup(r => r.UpdateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SlotOccupiedException("occupied"));

        var result = await _controller.Update(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    // ---- DELETE /api/featured-promo-items/{id} ----

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        _repository.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        _repository.Setup(r => r.DeleteAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.Delete(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- POST /api/featured-promo-items/{id}/move-up | move-down ----

    [Fact]
    public async Task MoveDown_SwapsToTheNextSlot()
    {
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Sample(1, slot: 1));
        _repository.Setup(r => r.SwapSlotAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.MoveDown(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _repository.Verify(r => r.SwapSlotAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoveUp_SwapsToThePreviousSlot()
    {
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Sample(1, slot: 3));
        _repository.Setup(r => r.SwapSlotAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.MoveUp(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task MoveUp_ReturnsBadRequest_WhenAlreadyOnFirstSlot()
    {
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Sample(1, slot: 1));

        var result = await _controller.MoveUp(1, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _repository.Verify(r => r.SwapSlotAsync(It.IsAny<int>(), It.IsAny<byte>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MoveDown_ReturnsBadRequest_WhenAlreadyOnLastSlot()
    {
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Sample(1, slot: 3));

        var result = await _controller.MoveDown(1, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Move_ReturnsNotFound_WhenRowIsMissing()
    {
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((FeaturedPromoItem?)null);

        var result = await _controller.MoveDown(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ---- Request validation (what [ApiController] turns into 400) ----

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Request_IsValid_WhenAllFieldsPresent()
    {
        Assert.Empty(Validate(SampleRequest()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Request_IsInvalid_WhenSlotIsOutOfRange(byte slot)
    {
        var request = SampleRequest();
        request.Slot = slot;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(FeaturedPromoItemRequest.Slot)));
    }

    [Fact]
    public void Request_IsInvalid_WhenTrainingCenterOrPromotionIsZero()
    {
        var request = SampleRequest();
        request.TrainingCenterPkid = 0;
        request.PromotionPkid = 0;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(FeaturedPromoItemRequest.TrainingCenterPkid)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(FeaturedPromoItemRequest.PromotionPkid)));
    }

    [Theory]
    [InlineData(nameof(FeaturedPromoItemRequest.Topic))]
    [InlineData(nameof(FeaturedPromoItemRequest.Description))]
    public void Request_IsInvalid_WhenRequiredStringIsBlank(string member)
    {
        var request = SampleRequest();
        typeof(FeaturedPromoItemRequest).GetProperty(member)!.SetValue(request, "   ");

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData(nameof(FeaturedPromoItemRequest.Topic), 101)]
    [InlineData(nameof(FeaturedPromoItemRequest.Description), 301)]
    public void Request_IsInvalid_WhenStringExceedsMaxLength(string member, int length)
    {
        var request = SampleRequest();
        typeof(FeaturedPromoItemRequest).GetProperty(member)!.SetValue(request, new string('名', length));

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(member));
    }
}
