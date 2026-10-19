using System.Net;
using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Where an outbound webhook may be posted. The server posts it, so an address inside
/// its own network — the cloud metadata service above all — must never qualify.
/// </summary>
public class PublicAddressTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.5.4")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void InternalAddressesAreNotPublic(string address) =>
        PublicAddress.IsPublic(IPAddress.Parse(address)).Should().BeFalse();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddressesArePublic(string address) =>
        PublicAddress.IsPublic(IPAddress.Parse(address)).Should().BeTrue();

    [Theory]
    [InlineData("https://hooks.example.com/x", true)]
    [InlineData("https://localhost/x", false)]
    [InlineData("https://api.localhost/x", false)]
    [InlineData("https://metadata.google.internal/x", false)]
    [InlineData("https://10.0.0.1/x", false)]
    [InlineData("https://[::1]/x", false)]
    public void ATypedUrlIsCheckedBeforeItIsSaved(string url, bool expected) =>
        PublicAddress.LooksPublic(new Uri(url)).Should().Be(expected);
}
