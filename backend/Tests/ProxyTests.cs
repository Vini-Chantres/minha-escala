using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MinhaEscala.Tests;

public sealed class ProxyTests
{
    [Theory]
    [InlineData(false, "http", "127.0.0.1")]
    [InlineData(true, "https", "203.0.113.15")]
    public async Task ForwardedHeadersAreTrustedOnlyWhenExplicitlyEnabled(bool enabled, string scheme, string ip)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["VERCEL_PROXY"] = enabled.ToString() }).Build();
        var context = new DefaultHttpContext(); context.Request.Scheme = "http"; context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["x-forwarded-proto"] = "https"; context.Request.Headers["x-vercel-forwarded-for"] = "203.0.113.15";
        var called = false;
        await new VercelProxyMiddleware(_ => { called = true; return Task.CompletedTask; }, config).InvokeAsync(context);
        Assert.True(called); Assert.Equal(scheme, context.Request.Scheme); Assert.Equal(ip, context.Connection.RemoteIpAddress!.ToString());
    }
    [Fact]
    public async Task InvalidForwardedValuesDoNotChangeConnection()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["VERCEL_PROXY"] = "true" }).Build();
        var context = new DefaultHttpContext(); context.Request.Scheme = "http"; context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["x-forwarded-proto"] = "javascript"; context.Request.Headers["x-vercel-forwarded-for"] = "invalid";
        await new VercelProxyMiddleware(_ => Task.CompletedTask, config).InvokeAsync(context);
        Assert.Equal("http", context.Request.Scheme); Assert.Equal(IPAddress.Loopback, context.Connection.RemoteIpAddress);
    }
}
