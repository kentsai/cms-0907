using System.ComponentModel.DataAnnotations;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class CourseGroupsControllerTests
{
    private readonly Mock<ICourseGroupRepository> _repository = new(MockBehavior.Strict);
    private readonly CourseGroupsController _controller;

    public CourseGroupsControllerTests()
    {
        _controller = new CourseGroupsController(_repository.Object);
    }

    private static CourseGroup Sample(short pkid = 1) => new()
    {
        Pkid = pkid,
        Description = "雲端運算"
    };

    private static CourseGroupRequest SampleRequest(short pkid = 0) => new()
    {
        Pkid = pkid,
        Description = "雲端運算"
    };

    // ---- GET /api/course-groups ----

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<CourseGroup> { Sample(1), Sample(2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    // ---- POST /api/course-groups/query ----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsOk()
    {
        var query = new CourseGroupQuery { Keyword = "雲端" };
        var expected = new List<CourseGroup> { Sample(1) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- GET /api/course-groups/{id} ----

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
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((CourseGroup?)null);

        var result = await _controller.GetById(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- POST /api/course-groups ----

    [Fact]
    public async Task Create_ReturnsCreatedAtGetById_WithAssignedPkid()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync((short)7);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(CourseGroupsController.GetById), created.ActionName);
        Assert.Equal((short)7, created.RouteValues!["id"]);

        var body = Assert.IsType<CourseGroup>(created.Value);
        Assert.Equal(7, body.Pkid);
        Assert.Equal("雲端運算", body.Description);
    }

    // ---- PUT /api/course-groups ----

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

    // ---- DELETE /api/course-groups/{id} ----

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
    public void Request_IsValid_WhenDescriptionPresent()
    {
        Assert.Empty(Validate(SampleRequest()));
    }

    [Fact]
    public void Request_IsValid_WhenDescriptionIsExactly100Chars()
    {
        var request = SampleRequest();
        request.Description = new string('名', 100);

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_IsInvalid_WhenDescriptionIsBlank(string description)
    {
        var request = SampleRequest();
        request.Description = description;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseGroupRequest.Description)));
    }

    [Fact]
    public void Request_IsInvalid_WhenDescriptionExceeds100Chars()
    {
        var request = SampleRequest();
        request.Description = new string('名', 101);

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseGroupRequest.Description)));
    }
}
