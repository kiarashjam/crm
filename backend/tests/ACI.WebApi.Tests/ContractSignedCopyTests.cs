using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A contract signed outside CadenceE (on paper, or with another signing service) is recorded on
/// its deal by uploading the signed copy: the contract step is done as a countersignature does
/// it (the deal won and moved to its won stage), the copy is kept with the deal and served as
/// the contract's PDF, and nothing is emailed. A contract drafted or sent here and then signed
/// another way is closed by its copy the same way, and its signing link stops working.
/// </summary>
public class ContractSignedCopyTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << >>\n%%EOF");

    private sealed class App : CustomWebApplicationFactory
    {
        public readonly MailServer Mail = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton(Mail.Mock.Object));
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected}, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed record Workspace(HttpClient Owner, Guid Org, Guid Proposal, Guid Signed);

    /// <summary>A workspace with a pipeline whose last stage is won, and Anna's deal in its Proposal stage.</summary>
    private static async Task<(Workspace Ws, Guid Deal)> DealAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        owner.In(org);
        var pipeline = (await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Memberships" })), "pipeline")).GetProperty("id").GetGuid();
        async Task<Guid> Stage(string name, int order, bool isWon = false) =>
            (await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId = pipeline, name, displayOrder = order, isWon })), name)).GetProperty("id").GetGuid();
        var proposal = await Stage("Proposal", 1);
        var signed = await Stage("Signed", 2, isWon: true);
        var lead = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = "anna@example.ch", status = "New" })), "lead")).GetProperty("id").GetGuid();
        var converted = await Json(await owner.PostAsync($"/api/leads/{lead}/convert",
            Body(new { createContact = true, createDeal = true, dealName = "Gold membership Anna", dealAmount = 4800m })), "convert");
        var deal = converted.GetProperty("dealId").GetGuid();
        await Json(await owner.PutAsync($"/api/deals/{deal}", Body(new { dealStageId = proposal })), "to Proposal");
        return (new Workspace(owner, org, proposal, signed), deal);
    }

    private static MultipartFormDataContent Upload(byte[] file, string fileName, string contentType, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, "file", fileName);
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        return form;
    }

    [Fact]
    public async Task UploadingTheSignedCopy_ExecutesTheContract_WinsTheDeal_AndKeepsTheCopyWithTheDeal()
    {
        await using var app = new App();
        var (ws, deal) = await DealAsync(app);

        var contract = await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(Pdf, "Contrat Anna signé.pdf", "application/pdf",
            ("dealId", deal.ToString()), ("signedBy", "Anna Muster"), ("signedOn", "2026-11-10"))), "upload the signed copy");

        contract.GetProperty("status").GetString().Should().Be("countersigned");
        contract.GetProperty("signedElsewhere").GetBoolean().Should().BeTrue();
        contract.GetProperty("dealId").GetGuid().Should().Be(deal);
        contract.GetProperty("clientSignatureName").GetString().Should().Be("Anna Muster");
        contract.GetProperty("clientSignedAtUtc").GetDateTime().Date.Should().Be(new DateTime(2026, 11, 10));
        contract.GetProperty("counterpartyName").GetString().Should().Be("Anna Muster");
        contract.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("type").GetString()).Should().Contain(["signed_elsewhere", "deal_won"]);

        // The contract step is done: the deal is won, in its pipeline's won stage.
        var won = await Json(await ws.Owner.GetAsync($"/api/deals/{deal}"), "the deal");
        (won.GetProperty("isWon").GetBoolean(), won.GetProperty("dealStageId").GetGuid()).Should().Be((true, ws.Signed));

        // The copy is a file on the deal, and the contract's PDF is that very file.
        var files = (await Json(await ws.Owner.GetAsync($"/api/attachments?entityType=deal&recordId={deal}"), "the deal's files")).EnumerateArray().ToList();
        files.Should().ContainSingle().Which.GetProperty("fileName").GetString().Should().Be("Contrat Anna signé.pdf");
        var pdf = await ws.Owner.GetAsync($"/api/contracts/{contract.GetProperty("id").GetGuid()}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync()).Should().Equal(Pdf);

        // Signed and passed on outside CadenceE: nobody is emailed it, and there is no copy to send again.
        app.Mail.Attempts.Should().BeEmpty();
        await Json(await ws.Owner.PostAsync($"/api/contracts/{contract.GetProperty("id").GetGuid()}/resend-copy", null), "resend", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AContractSentHereButSignedAnotherWay_IsClosedByItsCopy_AndItsSigningLinkStopsWorking()
    {
        await using var app = new App();
        var (ws, deal) = await DealAsync(app);
        var draft = await Json(await ws.Owner.PostAsync("/api/contracts/draft", Body(new
        {
            dealId = deal,
            values = new Dictionary<string, string>
            {
                ["contract.paymentTerms"] = "annually in advance", ["contract.startDate"] = "1 December 2026", ["contract.term"] = "12 months",
                ["contract.noticePeriod"] = "three months'", ["contract.jurisdiction"] = "Switzerland",
            },
        })), "draft");
        var id = draft.GetProperty("id").GetGuid();
        var token = (await Json(await ws.Owner.PostAsync($"/api/contracts/{id}/send", null), "send")).GetProperty("signingUrl").GetString()!.Split("/sign/").Last();

        var closed = await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(Pdf, "signed.pdf", "application/pdf",
            ("contractId", id.ToString()))), "upload its signed copy");

        closed.GetProperty("id").GetGuid().Should().Be(id, "the copy closes the contract that was sent, rather than adding a second");
        closed.GetProperty("status").GetString().Should().Be("countersigned");
        closed.GetProperty("clientSignatureName").GetString().Should().Be("Anna Muster", "the counterparty signed it when nobody else is named");
        (await Json(await ws.Owner.GetAsync($"/api/contracts/for-deal/{deal}"), "the deal's contracts")).GetArrayLength().Should().Be(1);
        (await Json(await ws.Owner.GetAsync($"/api/deals/{deal}"), "the deal")).GetProperty("isWon").GetBoolean().Should().BeTrue();

        using var anon = app.CreateClient();
        (await anon.GetAsync($"/api/public/contracts/{token}")).IsSuccessStatusCode.Should().BeFalse("it was signed another way, so its link signs nothing");

        // Executed now: a second copy cannot close it again.
        await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(Pdf, "again.pdf", "application/pdf", ("contractId", id.ToString()))),
            "upload again", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OnlyAPdfOrAPhotoIsTaken_SignedNoLaterThanToday_ForADealOrLeadOfTheWorkspace()
    {
        await using var app = new App();
        var (ws, deal) = await DealAsync(app);
        var text = Encoding.ASCII.GetBytes("just some words, not a contract");

        await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(text, "contract.pdf", "application/pdf", ("dealId", deal.ToString()))),
            "a text file named .pdf", HttpStatusCode.BadRequest);
        await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(Pdf, "contract.docx", "application/pdf", ("dealId", deal.ToString()))),
            "a PDF named .docx", HttpStatusCode.BadRequest);
        await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(Pdf, "contract.pdf", "application/pdf",
            ("dealId", deal.ToString()), ("signedOn", DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd")))), "signed next week", HttpStatusCode.BadRequest);
        await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(Pdf, "contract.pdf", "application/pdf")),
            "for nothing", HttpStatusCode.BadRequest);
        (await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(Pdf, "contract.pdf", "application/pdf", ("dealId", Guid.NewGuid().ToString()))))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A JPEG photo of the signed pages is a signed copy too.
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        (await Json(await ws.Owner.PostAsync("/api/contracts/signed-copy", Upload(jpeg, "signed-page.jpg", "image/jpeg", ("dealId", deal.ToString()))),
            "a photo")).GetProperty("status").GetString().Should().Be("countersigned");

        (await Json(await ws.Owner.GetAsync($"/api/deals/{deal}"), "the deal")).GetProperty("isWon").GetBoolean().Should().BeTrue();
        (await app.Services.Read(db => db.Attachments.Count())).Should().Be(1, "nothing refused was kept");
    }
}
