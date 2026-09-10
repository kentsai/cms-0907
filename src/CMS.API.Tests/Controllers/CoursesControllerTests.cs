using System.ComponentModel.DataAnnotations;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class CoursesControllerTests
{
    private readonly Mock<ICourseRepository> _repository = new(MockBehavior.Strict);
    private readonly CoursesController _controller;

    public CoursesControllerTests()
    {
        _controller = new CoursesController(_repository.Object);
    }

    private static Course Sample(int pkid = 1) => new()
    {
        Pkid = pkid,
        Title = "Azure 管理員",
        OfficialTitle = "Microsoft Azure Administrator",
        CourseId = "AZ-104",
        ProdCourseId = "AZ104",
        FriendlyUrl = "azure-administrator",
        DisplayOrder = 10,
        PartnerPkid = 1,
        CourseGroupPkid = 2,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 1, 1),
        ScheduleOff = new DateOnly(2036, 1, 1),
        Hour = 24,
        ListPrice = 30000,
        LearningCredit = 3.5m,
        CanRepeat = true,
        PartnerName = "Microsoft",
        CourseGroupDescription = "雲端",
        PublishStatusDescription = "已發布",
        CertificationPkids = [5],
        JobCategoryPkids = [1, 2]
    };

    private static CourseRequest SampleRequest(int pkid = 0) => new()
    {
        Pkid = pkid,
        Title = "Azure 管理員",
        OfficialTitle = "Microsoft Azure Administrator",
        CourseId = "AZ-104",
        ProdCourseId = "AZ104",
        FriendlyUrl = "azure-administrator",
        DisplayOrder = 10,
        PartnerPkid = 1,
        CourseGroupPkid = 2,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 1, 1),
        ScheduleOff = new DateOnly(2036, 1, 1),
        Hour = 24,
        ListPrice = 30000,
        LearningCredit = 3.5m,
        CanRepeat = true,
        CertificationPkids = [5],
        JobCategoryPkids = [1, 2]
    };

    // ---- GET /api/courses ----

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<Course> { Sample(1), Sample(2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    // ---- POST /api/courses/query ----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsOk()
    {
        var query = new CourseQuery { Keyword = "AZ", PartnerPkid = 1, CanRepeat = true, ScheduleOnFrom = new DateOnly(2026, 1, 1) };
        var expected = new List<Course> { Sample(1) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- GET /api/courses/{id} ----

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
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((Course?)null);

        var result = await _controller.GetById(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- POST /api/courses ----

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
        Assert.Equal(nameof(CoursesController.GetById), created.ActionName);
        Assert.Equal(7, created.RouteValues!["id"]);
        Assert.Same(stored, created.Value);
    }

    [Fact]
    public async Task Create_FallsBackToEchoingRequest_WhenReReadReturnsNull()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(8);
        _repository.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync((Course?)null);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var body = Assert.IsType<Course>(created.Value);
        Assert.Equal(8, body.Pkid);
        Assert.Equal("AZ-104", body.CourseId);
        Assert.Equal([5], body.CertificationPkids);
    }

    // ---- PUT /api/courses ----

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

    // ---- DELETE /api/courses/{id} ----

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

    [Fact]
    public void Request_IsValid_WhenOptionalFieldsAreNull()
    {
        var request = SampleRequest();
        request.OfficialTitle = null;
        request.CourseGroupPkid = null;
        request.Material = null;
        request.Outline = null;
        request.CertificationPkids = [];
        request.JobCategoryPkids = [];

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(nameof(CourseRequest.Title))]
    [InlineData(nameof(CourseRequest.CourseId))]
    [InlineData(nameof(CourseRequest.ProdCourseId))]
    [InlineData(nameof(CourseRequest.FriendlyUrl))]
    public void Request_IsInvalid_WhenRequiredStringIsBlank(string member)
    {
        var request = SampleRequest();
        typeof(CourseRequest).GetProperty(member)!.SetValue(request, "   ");

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData(nameof(CourseRequest.Title), 201)]
    [InlineData(nameof(CourseRequest.OfficialTitle), 301)]
    [InlineData(nameof(CourseRequest.FriendlyUrl), 101)]
    [InlineData(nameof(CourseRequest.Material), 501)]
    [InlineData(nameof(CourseRequest.Note), 4001)]
    public void Request_IsInvalid_WhenNvarcharExceedsMaxLength(string member, int length)
    {
        var request = SampleRequest();
        typeof(CourseRequest).GetProperty(member)!.SetValue(request, new string('名', length));

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData("AZ 104")]      // whitespace
    [InlineData("課程")]        // non-ASCII in a varchar column
    public void Request_IsInvalid_WhenCourseIdIsNotAscii(string courseId)
    {
        var request = SampleRequest();
        request.CourseId = courseId;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseRequest.CourseId)));
    }

    [Fact]
    public void Request_IsInvalid_WhenPartnerOrPublishStatusIsZero()
    {
        var request = SampleRequest();
        request.PartnerPkid = 0;
        request.PublishStatusPkid = 0;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseRequest.PartnerPkid)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseRequest.PublishStatusPkid)));
    }

    [Fact]
    public void Request_IsInvalid_WhenPriceOrCreditIsNegative()
    {
        var request = SampleRequest();
        request.ListPrice = -1;
        request.LearningCredit = -0.5m;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseRequest.ListPrice)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseRequest.LearningCredit)));
    }

    [Fact]
    public void Request_IsInvalid_WhenScheduleOffPrecedesScheduleOn()
    {
        var request = SampleRequest();
        request.ScheduleOn = new DateOnly(2026, 6, 1);
        request.ScheduleOff = new DateOnly(2026, 5, 31);

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CourseRequest.ScheduleOff)));
    }

    [Fact]
    public void Request_IsValid_WhenScheduleOffEqualsScheduleOn()
    {
        var request = SampleRequest();
        request.ScheduleOn = new DateOnly(2026, 6, 1);
        request.ScheduleOff = new DateOnly(2026, 6, 1);

        Assert.Empty(Validate(request));
    }
}
