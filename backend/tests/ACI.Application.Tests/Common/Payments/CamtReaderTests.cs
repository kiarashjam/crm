using System.Text;
using ACI.Application.Common.Payments;

namespace ACI.Application.Tests.Common.Payments;

/// <summary>
/// Reading the bank's camt files: the 2019 camt.054 a Swiss bank sends for QR-bill payments
/// (a batch of transactions, each with its reference), the older camt.053, and files that are
/// not bank files or try to read more than the file.
/// </summary>
public class CamtReaderTests
{
    private const string Qrr = "123400000000000000000000016";

    private static CamtFile Read(string xml) => CamtReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    private const string Notification = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.054.001.08">
          <BkToCstmrDbtCdtNtfctn>
            <GrpHdr><MsgId>MSG-2026-11-02</MsgId><CreDtTm>2026-11-02T18:00:00+01:00</CreDtTm></GrpHdr>
            <Ntfctn>
              <Id>N1</Id>
              <Acct><Id><IBAN>CH4431999123000889012</IBAN></Id></Acct>
              <Ntry>
                <Amt Ccy="CHF">350.00</Amt>
                <CdtDbtInd>CRDT</CdtDbtInd>
                <Sts><Cd>BOOK</Cd></Sts>
                <BookgDt><Dt>2026-11-02</Dt></BookgDt>
                <ValDt><Dt>2026-11-02</Dt></ValDt>
                <AcctSvcrRef>ENTRY-1</AcctSvcrRef>
                <NtryDtls>
                  <Btch><NbOfTxs>2</NbOfTxs></Btch>
                  <TxDtls>
                    <Refs><AcctSvcrRef>TX-1</AcctSvcrRef><EndToEndId>E2E-1</EndToEndId></Refs>
                    <Amt Ccy="CHF">299.00</Amt>
                    <CdtDbtInd>CRDT</CdtDbtInd>
                    <RltdPties><Dbtr><Pty><Nm>Jean Dupont</Nm></Pty></Dbtr><DbtrAcct><Id><IBAN>CH93 0076 2011 6238 5295 7</IBAN></Id></DbtrAcct></RltdPties>
                    <RmtInf><Strd><CdtrRefInf><Tp><CdOrPrtry><Prtry>QRR</Prtry></CdOrPrtry></Tp><Ref>123400000000000000000000016</Ref></CdtrRefInf></Strd></RmtInf>
                  </TxDtls>
                  <TxDtls>
                    <Refs><AcctSvcrRef>TX-2</AcctSvcrRef><EndToEndId>NOTPROVIDED</EndToEndId></Refs>
                    <Amt Ccy="CHF">51.00</Amt>
                    <RltdPties><Dbtr><Pty><Nm>Ana Silva</Nm></Pty></Dbtr></RltdPties>
                    <RmtInf><Ustrd>Dinner in October</Ustrd></RmtInf>
                  </TxDtls>
                </NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="CHF">40.00</Amt>
                <CdtDbtInd>CRDT</CdtDbtInd>
                <Sts><Cd>BOOK</Cd></Sts>
                <BookgDt><Dt>2026-11-03</Dt></BookgDt>
                <AcctSvcrRef>ENTRY-2</AcctSvcrRef>
                <NtryDtls><TxDtls><RmtInf><Ustrd>Paying RF18 5390 0754 7034 thanks</Ustrd></RmtInf></TxDtls></NtryDtls>
              </Ntry>
              <Ntry>
                <Amt Ccy="CHF">12.50</Amt>
                <CdtDbtInd>DBIT</CdtDbtInd>
                <Sts><Cd>BOOK</Cd></Sts>
                <BookgDt><Dt>2026-11-03</Dt></BookgDt>
                <AcctSvcrRef>FEE-1</AcctSvcrRef>
              </Ntry>
              <Ntry>
                <Amt Ccy="CHF">80.00</Amt>
                <CdtDbtInd>CRDT</CdtDbtInd>
                <Sts><Cd>PDNG</Cd></Sts>
                <BookgDt><Dt>2026-11-04</Dt></BookgDt>
                <AcctSvcrRef>PENDING-1</AcctSvcrRef>
              </Ntry>
            </Ntfctn>
          </BkToCstmrDbtCdtNtfctn>
        </Document>
        """;

    [Fact]
    public void ABatchIsReadAsItsTransactions_EachWithItsAmountReferenceAndPayer()
    {
        var file = Read(Notification);

        file.Format.Should().Be("camt.054");
        file.MessageId.Should().Be("MSG-2026-11-02");
        file.Accounts.Should().Equal("CH4431999123000889012");
        file.Transactions.Should().HaveCount(5);

        var qr = file.Transactions[0];
        qr.BankReference.Should().Be("CH4431999123000889012:TX-1", "the bank's reference for the transaction, with its account");
        qr.Amount.Should().Be(299m);
        qr.Currency.Should().Be("CHF");
        qr.IsCredit.Should().BeTrue();
        qr.Status.Should().Be("BOOK");
        qr.ReferenceType.Should().Be("QRR");
        qr.Reference.Should().Be(Qrr);
        qr.DebtorName.Should().Be("Jean Dupont");
        qr.DebtorIban.Should().Be("CH9300762011623852957");
        qr.BookedAtUtc.Should().Be(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        qr.EndToEndId.Should().Be("E2E-1");

        var plain = file.Transactions[1];
        plain.Amount.Should().Be(51m);
        plain.Reference.Should().BeNull();
        plain.Message.Should().Be("Dinner in October");
        plain.EndToEndId.Should().BeNull("NOTPROVIDED is no reference");
    }

    [Fact]
    public void ACreditorReferenceWrittenInTheFreeTextIsFound_AndDebitsAndPendingBookingsAreMarked()
    {
        var file = Read(Notification);

        var written = file.Transactions[2];
        written.ReferenceType.Should().Be("SCOR");
        written.Reference.Should().Be("RF18539007547034");
        written.Amount.Should().Be(40m, "a single transaction takes the entry's amount");
        written.BankReference.Should().Be("CH4431999123000889012:ENTRY-2", "with no reference of its own, the entry's");

        file.Transactions[3].IsCredit.Should().BeFalse();
        file.Transactions[4].Status.Should().Be("PDNG");
    }

    [Fact]
    public void TheSameFileGivesTheSameReferences_SoAReimportFindsWhatItBroughtIn()
    {
        Read(Notification).Transactions.Select(t => t.BankReference)
            .Should().Equal(Read(Notification).Transactions.Select(t => t.BankReference));
        Read(Notification).Transactions.Select(t => t.BankReference).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AnOlderCamt053StatementReadsAlike()
    {
        var file = Read("""
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.04">
              <BkToCstmrStmt>
                <GrpHdr><MsgId>STMT-1</MsgId></GrpHdr>
                <Stmt>
                  <Acct><Id><IBAN>CH5800791123000889012</IBAN></Id></Acct>
                  <Ntry>
                    <NtryRef>7</NtryRef>
                    <Amt Ccy="EUR">120.00</Amt>
                    <CdtDbtInd>CRDT</CdtDbtInd>
                    <Sts>BOOK</Sts>
                    <BookgDt><DtTm>2026-10-31T23:30:00+01:00</DtTm></BookgDt>
                    <NtryDtls><TxDtls>
                      <RltdPties><Dbtr><Nm>Lea Meier</Nm></Dbtr></RltdPties>
                      <RmtInf><Strd><CdtrRefInf><Tp><CdOrPrtry><Cd>SCOR</Cd></CdOrPrtry></Tp><Ref>RF18539007547034</Ref></CdtrRefInf></Strd></RmtInf>
                    </TxDtls></NtryDtls>
                  </Ntry>
                </Stmt>
              </BkToCstmrStmt>
            </Document>
            """);

        file.Format.Should().Be("camt.053");
        var credit = file.Transactions.Single();
        credit.Status.Should().Be("BOOK", "the older versions write the status as text");
        credit.Currency.Should().Be("EUR");
        credit.DebtorName.Should().Be("Lea Meier");
        credit.ReferenceType.Should().Be("SCOR");
        credit.BookedAtUtc.Should().Be(new DateTime(2026, 10, 31, 22, 30, 0, DateTimeKind.Utc));
        credit.BankReference.Should().Be("CH5800791123000889012:N:7");
    }

    [Fact]
    public void AFileThatIsNotABankFileIsRefusedInPlainWords()
    {
        var notXml = () => Read("IBAN;Amount\nCH58;10");
        notXml.Should().Throw<CamtException>().WithMessage("*not a camt XML file*");
        var otherXml = () => Read("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:pain.001.001.09\"><CstmrCdtTrfInitn/></Document>");
        otherXml.Should().Throw<CamtException>().WithMessage("*camt.054, camt.053 or camt.052*");
    }

    [Fact]
    public void AFileCannotReadOtherFilesOrServers()
    {
        // An external entity would read a file from the server, or call another one, into the
        // document: no DTD is read at all.
        var hostile = () => Read("""
            <?xml version="1.0"?>
            <!DOCTYPE Document [ <!ENTITY secret SYSTEM "file:///etc/passwd"> ]>
            <Document><BkToCstmrDbtCdtNtfctn><GrpHdr><MsgId>&secret;</MsgId></GrpHdr></BkToCstmrDbtCdtNtfctn></Document>
            """);
        hostile.Should().Throw<CamtException>();
    }
}
