using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IPTVGuideDog.Cli;
using IPTVGuideDog.Cli.Commands;
using IPTVGuideDog.Core.IO;
using IPTVGuideDog.Core.M3u;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IPTVGuideDog.Cli.Tests.Commands;

[TestClass]
public class RunCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_Throws_WhenPlaylistSourceMissing()
    {
        var cmd = CreateRunCommand();
        var context = new CommandContext(CommandKind.Run, new CommandOptionSet(new Dictionary<string, List<string>>()), null, null, null, new Dictionary<string, string>(), null, null, null, null, null, null, false, false);
        await Assert.ThrowsExceptionAsync<CliException>(() => cmd.ExecuteAsync(context, CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteAsync_Throws_WhenEpgRequestedAndNoOutput()
    {
        var cmd = CreateRunCommand();
        var context = new CommandContext(CommandKind.Run, new CommandOptionSet(new Dictionary<string, List<string>>()), null, null, null, new Dictionary<string, string>(), "playlist", "epg", null, null, null, null, false, false);
        await Assert.ThrowsExceptionAsync<CliException>(() => cmd.ExecuteAsync(context, CancellationToken.None));
    }

    [TestMethod]
    public async Task ExecuteAsync_WritesWarning_WhenNoChannelsMatched()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var diagnostics = new StringWriter();
        var httpClient = new HttpClient(new MockHttpMessageHandler("#EXTM3U\n"));
        var parser = new PlaylistParser();
        var cmd = new RunCommand(stdout, stderr, diagnostics, httpClient, parser);
        var context = new CommandContext(CommandKind.Run, new CommandOptionSet(new Dictionary<string, List<string>>()), null, null, null, new Dictionary<string, string>(), "http://test", null, null, null, null, null, false, false);
        var result = await cmd.ExecuteAsync(context, CancellationToken.None);
        Assert.IsTrue(stderr.ToString().Contains("Warning: no channels matched"));
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task WritePlaylistAsync_WritesToStdout_WhenOutputPathIsNull()
    {
        var stdout = new StringWriter();
        var cmd = CreateRunCommand(stdout);
        var doc = new PlaylistDocument(new List<string>{"#EXTM3U"}, new List<M3uEntry>{ new M3uEntry(new List<string>{"#EXTINF:-1,Test"}, "http://url") });
        await (Task)cmd.GetType().GetMethod("WritePlaylistAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(cmd, new object[]{ doc, doc.Entries, null, CancellationToken.None })!;
        Assert.IsTrue(stdout.ToString().Contains("#EXTM3U"));
    }

    [TestMethod]
    public async Task WriteEpgAsync_WritesToStdout_WhenOutputPathIsNull()
    {
        var stdout = new StringWriter();
        var cmd = CreateRunCommand(stdout);
        await (Task)cmd.GetType().GetMethod("WriteEpgAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(cmd, new object[]{ "<xml>epg</xml>", null, CancellationToken.None })!;
        Assert.IsTrue(stdout.ToString().Contains("<xml>epg</xml>"));
    }

    private static RunCommand CreateRunCommand(StringWriter? stdout = null)
    {
        return new RunCommand(stdout ?? new StringWriter(), new StringWriter(), new StringWriter(), new HttpClient(new MockHttpMessageHandler("#EXTM3U\n")), new PlaylistParser());
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        public MockHttpMessageHandler(string response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(_response) });
    }
}
