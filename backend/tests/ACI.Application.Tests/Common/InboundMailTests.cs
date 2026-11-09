using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>Reading what the mail service posts back: the reply's own words, and its delivery reports.</summary>
public class InboundMailTests
{
    [Theory]
    [InlineData("Yes please.\n\nOn Mon, 9 Nov 2026 at 10:00, Anna <anna@club.ch> wrote:\n> Hi Jean\n> Shall we?")]
    [InlineData("Yes please.\n\nOn Mon, 9 Nov 2026 at 10:00, Anna Muster <\nanna@club.ch> wrote:\n> Hi Jean")]
    [InlineData("Yes please.\n\nLe lun. 9 nov. 2026 à 10:00, Anna <anna@club.ch> a écrit :\n> Bonjour")]
    [InlineData("Yes please.\n\nAm Mo., 9. Nov. 2026 um 10:00 Uhr schrieb Anna <anna@club.ch>:\n> Hallo")]
    [InlineData("Yes please.\r\n\r\n-----Original Message-----\r\nFrom: Anna\r\nSent: Monday")]
    [InlineData("Yes please.\n\n________________________________\nFrom: Anna <anna@club.ch>\nSent: Monday")]
    [InlineData("Yes please.\n> Hi Jean")]
    public void TheQuotedConversationIsLeftOut(string text)
    {
        InboundMail.WithoutQuotedHistory(text).Should().Be("Yes please.");
    }

    [Fact]
    public void ALineThatOnlyStartsLikeAnAttributionIsKept()
    {
        InboundMail.WithoutQuotedHistory("On Monday I can come.\nLe matin aussi.").Should().Be("On Monday I can come.\nLe matin aussi.");
    }

    [Fact]
    public void AReplyThatIsAllQuoteIsKeptWhole_AndALongOneIsCut()
    {
        InboundMail.WithoutQuotedHistory("> only a quote").Should().Be("> only a quote");
        InboundMail.WithoutQuotedHistory(new string('a', 50), max: 10).Should().Be(new string('a', 10));
        InboundMail.WithoutQuotedHistory(null).Should().BeEmpty();
    }

    [Fact]
    public void EventsAreReadOneByOne_AndAnUnreadableOneIsLeftOut()
    {
        var id = Guid.NewGuid();
        var events = InboundMail.ParseEvents($$"""
            [
              {"email":"jean@example.ch","event":"Bounce","type":"bounce","timestamp":1794200000,"reason":"550 no such user","cadence_message_id":"{{id}}","smtp-id":"<a@b>"},
              {"email":"anne@example.ch","event":"delivered","response":"250 OK"},
              {"email":"x@example.ch"},
              "not an event"
            ]
            """, "cadence_message_id");

        events.Should().HaveCount(2);
        events[0].Should().Be(new MailEvent("bounce", "jean@example.ch", DateTimeOffset.FromUnixTimeSeconds(1794200000).UtcDateTime, id, "<a@b>", "550 no such user", "bounce"));
        events[1].Reason.Should().Be("250 OK");
        events[1].OutboundMessageId.Should().BeNull();
        InboundMail.ParseEvents("{\"event\":\"bounce\"}", "cadence_message_id").Should().BeEmpty();
        InboundMail.ParseEvents("not json", "cadence_message_id").Should().BeEmpty();
    }

    [Fact]
    public void ASignatureIsCheckedOverTheTimestampAndTheBody()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var body = Encoding.UTF8.GetBytes("[{\"event\":\"delivered\"}]");
        var signature = Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes("1794200000").Concat(body).ToArray(),
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));

        InboundMail.IsSignedBy(publicKey, signature, "1794200000", body).Should().BeTrue();
        InboundMail.IsSignedBy(publicKey, signature, "1794200001", body).Should().BeFalse("the timestamp is signed too");
        InboundMail.IsSignedBy(publicKey, signature, "1794200000", Encoding.UTF8.GetBytes("[]")).Should().BeFalse();
        InboundMail.IsSignedBy(publicKey, null, "1794200000", body).Should().BeFalse();
        InboundMail.IsSignedBy(publicKey, "not base64!", "1794200000", body).Should().BeFalse();
        InboundMail.IsSignedBy("not a key", signature, "1794200000", body).Should().BeFalse();
    }
}
