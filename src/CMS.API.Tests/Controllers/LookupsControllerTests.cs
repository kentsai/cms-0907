using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class LookupsControllerTests
{
    [Fact]
    public async Task PublishStatuses_ReturnsOk_WithLookupItems()
    {
        var expected = new List<LookupItem>
        {
            new() { Pkid = 1, Label = "草稿" },
            new() { Pkid = 2, Label = "已發布" }
        };
        var repository = new Mock<IPublishStatusRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = new LookupsController(repository.Object);

        var result = await controller.PublishStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<LookupItem>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.Equal("草稿", items[0].Label);
    }
}
