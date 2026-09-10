using System.ComponentModel.DataAnnotations;
using CMS.API.Controllers;
using CMS.API.Infrastructure;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class PartnersControllerTests
{
    private readonly Mock<IPartnerRepository> _repository = new(MockBehavior.Strict);
    private readonly PartnersController _controller;

    public PartnersControllerTests()
    {
        _controller = new PartnersController(_repository.Object);
    }

    private static Partner Sample(short pkid = 1) => new()
    {
        Pkid = pkid,
        Name = "Microsoft",
        AppKey = "MS",
        NameOnPartnerMenu = "Microsoft 微軟",
        NameOnCourseDetailPage = "微軟",
        DisplayOrder = 10,
        ImageFilename = "microsoft.png"
    };

    private static PartnerRequest SampleRequest(short pkid = 0) => new()
    {
        Pkid = pkid,
        Name = "Microsoft",
        AppKey = "MS",
        NameOnPartnerMenu = "Microsoft 微軟",
        NameOnCourseDetailPage = "微軟",
        DisplayOrder = 10,
        ImageFilename = "microsoft.png"
    };

    // ---- GET /api/partners ----

    [Fact]
    public async Task GetAll_ReturnsOk_WithRepositoryList()
    {
        var expected = new List<Partner> { Sample(1), Sample(2) };
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    // ---- POST /api/partners/query ----

    [Fact]
    public async Task Query_PassesFilterThrough_AndReturnsOk()
    {
        var query = new PartnerQuery { Keyword = "Micro" };
        var expected = new List<Partner> { Sample(1) };
        _repository.Setup(r => r.QueryAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Query(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        _repository.Verify(r => r.QueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- GET /api/partners/{id} ----

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
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((Partner?)null);

        var result = await _controller.GetById(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---- POST /api/partners ----

    [Fact]
    public async Task Create_ReturnsCreatedAtGetById_WithAssignedPkid()
    {
        var request = SampleRequest();
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync((short)7);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(PartnersController.GetById), created.ActionName);
        Assert.Equal((short)7, created.RouteValues!["id"]);

        var body = Assert.IsType<Partner>(created.Value);
        Assert.Equal(7, body.Pkid);
        Assert.Equal("Microsoft", body.Name);
        Assert.Equal("MS", body.AppKey);
        Assert.Equal(10, body.DisplayOrder);
        Assert.Equal("microsoft.png", body.ImageFilename);
    }

    [Fact]
    public async Task Create_NormalisesBlankImageFilenameToNull()
    {
        var request = SampleRequest();
        request.ImageFilename = "   ";
        _repository.Setup(r => r.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync((short)8);

        var result = await _controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var body = Assert.IsType<Partner>(created.Value);
        Assert.Null(body.ImageFilename);
    }

    // ---- PUT /api/partners ----

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

    // ---- DELETE /api/partners/{id} ----

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
    public void Request_IsValid_WhenImageFilenameIsNull()
    {
        var request = SampleRequest();
        request.ImageFilename = null;

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(nameof(PartnerRequest.Name))]
    [InlineData(nameof(PartnerRequest.AppKey))]
    [InlineData(nameof(PartnerRequest.NameOnPartnerMenu))]
    [InlineData(nameof(PartnerRequest.NameOnCourseDetailPage))]
    public void Request_IsInvalid_WhenRequiredStringIsBlank(string member)
    {
        var request = SampleRequest();
        typeof(PartnerRequest).GetProperty(member)!.SetValue(request, "   ");

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData(nameof(PartnerRequest.Name), 51)]
    [InlineData(nameof(PartnerRequest.NameOnPartnerMenu), 201)]
    [InlineData(nameof(PartnerRequest.NameOnCourseDetailPage), 51)]
    public void Request_IsInvalid_WhenNvarcharExceedsMaxLength(string member, int length)
    {
        var request = SampleRequest();
        typeof(PartnerRequest).GetProperty(member)!.SetValue(request, new string('名', length));

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData("ABCDEFGHIJK")]   // 11 chars > varchar(10)
    [InlineData("微軟")]          // non-ASCII in a varchar column
    [InlineData("M S")]           // whitespace
    public void Request_IsInvalid_WhenAppKeyIsTooLongOrNotAscii(string appKey)
    {
        var request = SampleRequest();
        request.AppKey = appKey;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PartnerRequest.AppKey)));
    }

    [Theory]
    [InlineData("微軟.png")]
    [InlineData("my logo.png")]
    public void Request_IsInvalid_WhenImageFilenameIsNotAscii(string imageFilename)
    {
        var request = SampleRequest();
        request.ImageFilename = imageFilename;

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PartnerRequest.ImageFilename)));
    }

    [Fact]
    public void Request_IsInvalid_WhenImageFilenameExceeds50Chars()
    {
        var request = SampleRequest();
        request.ImageFilename = new string('a', 47) + ".png";

        var errors = Validate(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(PartnerRequest.ImageFilename)));
    }
}
