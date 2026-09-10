using System.ComponentModel.DataAnnotations;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class CertificationsControllerTests
{
    private readonly Mock<ICertificationRepository> _repository = new(MockBehavior.Strict);
    private readonly CertificationsController _controller;

    public CertificationsControllerTests()
    {
        _controller = new CertificationsController(_repository.Object);
    }

    private static Certification Sample(int pkid = 1) => new()
    {
        Pkid = pkid,
        PartnerPkid = 1,
        Title = "Azure Administrator Associate",
        PartnerName = "Microsoft",
        CoursePkids = [10, 11],
        JobCategoryPkids = [1]
    };

    private static CertificationRequest SampleRequest(int pkid = 0) => new()
    {
        Pkid = pkid,
        PartnerPkid = 1,
        Title = "Azure Administrator Associate",
        CoursePkids = [10, 11],
        JobCategoryPkids = [1]
    };

    // ---- GET /api/certifications ----

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<Certification> { Sample(1), Sample(2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    // ---- POST /api/certifications/query ----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsOk()
    {
        var query = new CertificationQuery { Keyword = "Azure", PartnerPkid = 1, CoursePkid = 10, JobCategoryPkid = 1 };
        var expected = new List<Certification> { Sample(1) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- GET /api/certifications/{id} ----

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
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((Certification?)null);

        var result = await _controller.GetById(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- POST /api/certifications ----

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
        Assert.Equal(nameof(CertificationsController.GetById), created.ActionName);
        Assert.Equal(7, created.RouteValues!["id"]);
        Assert.Same(stored, created.Value);
    }

    [Fact]
    public async Task Create_FallsBackToEchoingRequest_WhenReReadReturnsNull()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(8);
        _repository.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync((Certification?)null);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var body = Assert.IsType<Certification>(created.Value);
        Assert.Equal(8, body.Pkid);
        Assert.Equal((short)1, body.PartnerPkid);
        Assert.Equal("Azure Administrator Associate", body.Title);
        Assert.Equal([10, 11], body.CoursePkids);
        Assert.Equal([(short)1], body.JobCategoryPkids);
    }

    // ---- PUT /api/certifications ----

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

    // ---- DELETE /api/certifications/{id} ----

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
    public void Request_IsValid_WhenTitleIsNull_AndIdListsAreEmpty()
    {
        var request = SampleRequest();
        request.Title = null;
        request.CoursePkids = [];
        request.JobCategoryPkids = [];

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Request_IsValid_WhenTitleIsExactly100Chars()
    {
        var request = SampleRequest();
        request.Title = new string('A', 100);

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Request_IsInvalid_WhenTitleExceeds100Chars()
    {
        var request = SampleRequest();
        request.Title = new string('名', 101);

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CertificationRequest.Title)));
    }

    [Fact]
    public void Request_IsInvalid_WhenPartnerIsZero()
    {
        var request = SampleRequest();
        request.PartnerPkid = 0;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CertificationRequest.PartnerPkid)));
    }
}
