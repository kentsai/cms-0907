using System.ComponentModel.DataAnnotations;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class PublishStatusesControllerTests
{
    private readonly Mock<IPublishStatusRepository> _repository = new(MockBehavior.Strict);
    private readonly PublishStatusesController _controller;

    public PublishStatusesControllerTests()
    {
        _controller = new PublishStatusesController(_repository.Object);
    }

    private static PublishStatus Sample(byte pkid = 1) => new()
    {
        Pkid = pkid,
        Description = "草稿",
        IsDraft = true,
        IsPublished = false,
        IsDiscontinued = false
    };

    private static PublishStatusRequest SampleRequest(byte pkid = 1) => new()
    {
        Pkid = pkid,
        Description = "草稿",
        IsDraft = true
    };

    // ---- GET /api/publish-statuses ----

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<PublishStatus> { Sample(1), Sample(2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    // ---- POST /api/publish-statuses/query ----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsOk()
    {
        var query = new PublishStatusQuery { Keyword = "草", IsDraft = true };
        var expected = new List<PublishStatus> { Sample(1) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- GET /api/publish-statuses/{id} ----

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
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((PublishStatus?)null);

        var result = await _controller.GetById(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- POST /api/publish-statuses ----

    [Fact]
    public async Task Create_ReturnsCreatedAtGetById_WithEntity()
    {
        var request = SampleRequest(5);
        _repository.Setup(r => r.ExistsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync((byte)5);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(PublishStatusesController.GetById), created.ActionName);
        Assert.Equal((byte)5, created.RouteValues!["id"]);

        var body = Assert.IsType<PublishStatus>(created.Value);
        Assert.Equal(5, body.Pkid);
        Assert.Equal("草稿", body.Description);
        Assert.True(body.IsDraft);
    }

    [Fact]
    public async Task Create_ReturnsConflict_WhenPkidAlreadyExists()
    {
        var request = SampleRequest(1);
        _repository.Setup(r => r.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.Create(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        _repository.Verify(r => r.CreateAsync(It.IsAny<PublishStatusRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The ExistsAsync check above is a read followed by a write, so two callers racing on the same pkid
    /// both pass it and the second loses on PK_PublishingStatus. The table stays correct — the constraint
    /// does its job — but the loser used to surface as a 500 rather than the documented 409.
    /// </summary>
    [Fact]
    public async Task Create_Returns409_WhenAConcurrentCreateWonTheRace()
    {
        var request = SampleRequest(5);
        _repository.Setup(r => r.ExistsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateKeyException("主代碼 5 已存在。"));

        var result = await _controller.Create(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    // ---- PUT /api/publish-statuses ----

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

    // ---- DELETE /api/publish-statuses/{id} ----

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

    [Fact]
    public async Task Delete_ReturnsConflict_WhenRowIsReferencedByForeignKey()
    {
        _repository.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityInUseException("in use"));

        var result = await _controller.Delete(1, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    // ---- Request validation (what [ApiController] turns into 400) ----

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Request_IsValid_WhenRequiredFieldsPresent()
    {
        Assert.Empty(Validate(SampleRequest()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_IsInvalid_WhenDescriptionMissing(string description)
    {
        var request = SampleRequest();
        request.Description = description;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PublishStatusRequest.Description)));
    }

    [Fact]
    public void Request_IsInvalid_WhenDescriptionExceeds50Chars()
    {
        var request = SampleRequest();
        request.Description = new string('狀', 51);

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PublishStatusRequest.Description)));
    }

    [Fact]
    public void Request_AllowsFullTinyintRange()
    {
        var min = SampleRequest(0);
        var max = SampleRequest(255);

        Assert.Empty(Validate(min));
        Assert.Empty(Validate(max));
    }
}
