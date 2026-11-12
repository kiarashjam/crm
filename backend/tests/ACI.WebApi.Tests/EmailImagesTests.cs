using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Images for the welcome email: kept by the workspace, served at a public address any
/// mail app can fetch, and nothing but real images.
/// </summary>
public class EmailImagesTests
{
    /// <summary>A real 1×1 PNG.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent Form(byte[] bytes, string name, string type, int? width = null)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        var form = new MultipartFormDataContent { { file, "file", name } };
        if (width is int w) form.Add(new StringContent(w.ToString()), "width");
        return form;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task AnUploadedImageIsServedToAnyoneAtItsAddress()
    {
        await using var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);

        var res = await owner.PostAsync("/api/email-images", Form(Png, "Logo du Pavillon.png", "image/png", width: 600));
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        var image = await JsonAsync(res);
        var url = image.GetProperty("url").GetString()!;
        url.Should().EndWith($"/api/public/email-images/{image.GetProperty("id").GetGuid():D}.png");
        image.GetProperty("fileName").GetString().Should().Be("Logo du Pavillon.png");
        image.GetProperty("width").GetInt32().Should().Be(600);

        using var mailApp = app.CreateClient();
        var served = await mailApp.GetAsync(new Uri(url).PathAndQuery);
        served.StatusCode.Should().Be(HttpStatusCode.OK, "a mail app fetches it with no sign-in");
        served.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await served.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
        served.Headers.CacheControl!.ToString().Should().Contain("immutable");
        served.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        (await mailApp.GetAsync(new Uri(url).PathAndQuery.Replace(".png", ".gif"))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await JsonAsync(await owner.GetAsync("/api/email-images"));
        list.EnumerateArray().Select(i => i.GetProperty("url").GetString()).Should().Equal(url);

        (await owner.DeleteAsync($"/api/email-images/{image.GetProperty("id").GetGuid()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await mailApp.GetAsync(new Uri(url).PathAndQuery)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OnlyRealImagesAreKept_WhateverTheyAreCalled()
    {
        await using var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon 46"));

        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        (await owner.PostAsync("/api/email-images", Form(svg, "logo.svg", "image/svg+xml"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/email-images", Form(svg, "logo.png", "image/png"))).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "what a file is comes from its bytes, not its name");
        var html = "<html><script>alert(1)</script></html>"u8.ToArray();
        (await owner.PostAsync("/api/email-images", Form(html, "photo.jpg", "image/jpeg"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var huge = new byte[3 * 1024 * 1024 + 1];
        Png.CopyTo(huge, 0);
        (await owner.PostAsync("/api/email-images", Form(huge, "huge.png", "image/png"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A JPEG named .png is kept as the JPEG it is.
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var kept = await JsonAsync(await owner.PostAsync("/api/email-images", Form(jpeg, "photo.png", "image/png")));
        kept.GetProperty("contentType").GetString().Should().Be("image/jpeg");
        kept.GetProperty("url").GetString().Should().EndWith(".jpg");
        kept.GetProperty("fileName").GetString().Should().Be("photo.jpg");
    }

    [Fact]
    public async Task AnotherWorkspaceCannotListOrRemoveThem_AndAViewerCannotAdd()
    {
        await using var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        var id = (await JsonAsync(await owner.PostAsync("/api/email-images", Form(Png, "logo.png", "image/png")))).GetProperty("id").GetGuid();

        var (stranger, strangerId) = await SignInAsync(app, "stranger@example.ch");
        stranger.In(await CreateAsync(app, strangerId, "Elsewhere"));
        (await JsonAsync(await stranger.GetAsync("/api/email-images"))).EnumerateArray().Should().BeEmpty();
        (await stranger.DeleteAsync($"/api/email-images/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (viewer, viewerId) = await SignInAsync(app, "viewer@example.ch");
        await AddMemberAsync(app, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);
        (await viewer.PostAsync("/api/email-images", Form(Png, "logo.png", "image/png"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(await viewer.GetAsync("/api/email-images"))).EnumerateArray().Should().HaveCount(1, "everyone in the workspace sees its images");
    }
}
