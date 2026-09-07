using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CMS.API.Tests.Controllers;

public class LookupsControllerTests
{
    private readonly Mock<IPublishStatusRepository> _publishStatuses = new(MockBehavior.Strict);
    private readonly Mock<IAppRoleRepository> _appRoles = new(MockBehavior.Strict);
    private readonly Mock<IPartnerRepository> _partners = new(MockBehavior.Strict);
    private readonly Mock<ILookupRepository> _lookups = new(MockBehavior.Strict);
    private readonly LookupsController _controller;

    public LookupsControllerTests()
    {
        _controller = new LookupsController(_publishStatuses.Object, _appRoles.Object, _partners.Object, _lookups.Object);
    }

    [Fact]
    public async Task PublishStatuses_ReturnsOk_WithLookupItems()
    {
        var expected = new List<LookupItem>
        {
            new() { Pkid = 1, Label = "草稿" },
            new() { Pkid = 2, Label = "已發布" }
        };
        _publishStatuses.Setup(r => r.GetLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.PublishStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<LookupItem>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.Equal("草稿", items[0].Label);
    }

    [Fact]
    public async Task AppRoles_ReturnsOk_WithStringKeyedItems()
    {
        var expected = new List<StringLookupItem>
        {
            new() { Id = "Admin", Label = "Administrator" },
            new() { Id = "User", Label = "User" }
        };
        _appRoles.Setup(r => r.GetLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.AppRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<StringLookupItem>>(ok.Value);
        Assert.Equal("Admin", items[0].Id);
    }

    [Fact]
    public async Task Partners_ReturnsOk_WithLookupItems()
    {
        var expected = new List<LookupItem>
        {
            new() { Pkid = 1, Label = "Microsoft" },
            new() { Pkid = 2, Label = "Cisco" }
        };
        _partners.Setup(r => r.GetLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.Partners(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<LookupItem>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.Equal("Microsoft", items[0].Label);
    }

    [Fact]
    public async Task AppUsers_ReturnsOk_WithStringKeyedItems()
    {
        var expected = new List<StringLookupItem>
        {
            new() { Id = "helen", Label = "helen (helen)" }
        };
        _lookups.Setup(r => r.GetAppUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.AppUsers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<StringLookupItem>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("helen (helen)", items[0].Label);
    }
}
