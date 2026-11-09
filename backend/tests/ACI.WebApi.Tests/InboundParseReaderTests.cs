using ACI.Infrastructure.Services;

namespace ACI.WebApi.Tests;

/// <summary>A SendGrid Inbound Parse post read down to what matching a reply needs.</summary>
public class InboundParseReaderTests
{
    [Fact]
    public void TheParsedFieldsAndTheHeaderBlockAreRead()
    {
        var reply = InboundParseReader.Read(new InboundParseReader.Fields(
            Email: null,
            Headers: "Message-ID: <CAB1@mail.gmail.com>\r\nIn-Reply-To: <0f8fad5bd9cb469f@pavillon46.ch>\r\nReferences: <first@pavillon46.ch>\r\n <0f8fad5bd9cb469f@pavillon46.ch>",
            From: "\"Jean Dupont\" <Jean@Example.ch>",
            To: "Pavillon46 <info@pavillon46.ch>",
            Cc: "anna@pavillon46.ch",
            Subject: "Re: Hello",
            Text: "Yes please.",
            Html: null,
            Envelope: "{\"to\":[\"reply+abc@reply.cadence.example\"],\"from\":\"jean@example.ch\"}"))!;

        reply.FromAddress.Should().Be("Jean@Example.ch");
        reply.FromName.Should().Be("Jean Dupont");
        reply.Recipients.Should().Equal("reply+abc@reply.cadence.example", "info@pavillon46.ch", "anna@pavillon46.ch");
        reply.MessageId.Should().Be("<CAB1@mail.gmail.com>");
        reply.InReplyTo.Should().Be("<0f8fad5bd9cb469f@pavillon46.ch>");
        reply.References.Should().Equal("<first@pavillon46.ch>", "<0f8fad5bd9cb469f@pavillon46.ch>");
        reply.Text.Should().Be("Yes please.");
    }

    [Fact]
    public void TheRawMessageIsReadWhenSendGridPostsItWhole()
    {
        const string raw = "From: Jean Dupont <jean@example.ch>\r\nTo: reply+abc@reply.cadence.example\r\nSubject: Re: Hello\r\n"
            + "Message-ID: <raw-1@example.ch>\r\nIn-Reply-To: <0f8f@pavillon46.ch>\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nVolontiers, à demain.\r\n";

        var reply = InboundParseReader.Read(new InboundParseReader.Fields(raw, null, null, null, null, null, null, null, null))!;

        reply.FromAddress.Should().Be("jean@example.ch");
        reply.Recipients.Should().Equal("reply+abc@reply.cadence.example");
        reply.InReplyTo.Should().Be("<0f8f@pavillon46.ch>");
        reply.Text!.Trim().Should().Be("Volontiers, à demain.");
    }

    [Fact]
    public void AnHtmlOnlyReplyBecomesItsWords()
    {
        var reply = InboundParseReader.Read(new InboundParseReader.Fields(null, null, "jean@example.ch", "x@example.ch", null, "Re",
            null, "<html><head><style>p{color:red}</style></head><body><p>Yes &amp; thanks</p><div>Jean</div></body></html>", null))!;

        reply.Text.Should().Be("Yes & thanks\nJean");
    }

    [Fact]
    public void APostWithNoSenderIsNotAReply()
    {
        InboundParseReader.Read(new InboundParseReader.Fields(null, null, null, "x@example.ch", null, null, "Hi", null, null)).Should().BeNull();
    }

    [Fact]
    public void CommentsAreLeftOut_ATagThatOnlyStartsLikeHeadIsNot_AndALoneLessThanIsText()
    {
        HtmlReply("<!--[if mso]><xml>Outlook settings</xml><![endif]--><p>Yes</p><header>From the club</header><br/>1 < 2")
            .Should().Be("Yes\nFrom the club\n1 < 2");
    }

    [Theory]
    [InlineData("<style>", 200_000)]
    [InlineData("<", 1_000_000)]
    public void AnHtmlReplyBuiltToBeSlowIsReadInOnePass(string piece, int length)
    {
        // Anyone can send mail to the reply domain, and every post is read before it is
        // matched. Two hundred kilobytes of "<style>" took the old patterns most of a
        // minute, and a megabyte of it would have held the request for a quarter of an hour.
        var html = "Yes please" + string.Concat(Enumerable.Repeat(piece, length / piece.Length));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var text = HtmlReply(html);

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        text.Should().StartWith("Yes please");
    }

    /// <summary>The words read from a reply that has an HTML part only.</summary>
    private static string? HtmlReply(string html) =>
        InboundParseReader.Read(new InboundParseReader.Fields(null, null, "jean@example.ch", "x@example.ch", null, "Re", null, html, null))!.Text;
}
