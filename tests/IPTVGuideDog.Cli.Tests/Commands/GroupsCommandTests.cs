using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
public class GroupsCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_CreatesNewFile_WhenFileDoesNotExist()
    {
        var tmpFile = Path.GetTempFileName();
        File.Delete(tmpFile); // Delete so we can test creation
        
        try
        {
            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 2"" group-title=""News"",Channel 2
http://example.com/2";

            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups, 
                new CommandOptionSet(new Dictionary<string, List<string>>()), 
                null, null, null, 
                new Dictionary<string, string>(), 
                "http://test", 
                null, null, 
                tmpFile, 
                null, null, 
                false, false);
            
            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            Assert.AreEqual(0, result);
            Assert.IsTrue(File.Exists(tmpFile));
            
            var lines = await File.ReadAllLinesAsync(tmpFile);
            Assert.IsTrue(lines.Length > 3); // Header + groups
            Assert.IsTrue(lines.Contains("News"));
            Assert.IsTrue(lines.Contains("Sports"));
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_AddsOnlyNewGroups_WhenFileExists()
    {
        var tmpFile = Path.GetTempFileName();
        
        try
        {
            // Create existing file with some groups - use version 0.40 to match current major version
            var existingContent = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 0.40 ######

#Sports
News
Entertainment";
            await File.WriteAllTextAsync(tmpFile, existingContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 2"" group-title=""News"",Channel 2
http://example.com/2
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 3"" group-title=""Movies"",Channel 3
http://example.com/3";

            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups, 
                new CommandOptionSet(new Dictionary<string, List<string>>()), 
                null, null, null, 
                new Dictionary<string, string>(), 
                "http://test", 
                null, null, 
                tmpFile, 
                null, null, 
                false, false);
            
            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            Assert.AreEqual(0, result);
            
            var lines = await File.ReadAllLinesAsync(tmpFile);
            
            // Existing groups should still be there
            Assert.IsTrue(lines.Contains("#Sports"));
            Assert.IsTrue(lines.Contains("News"));
            Assert.IsTrue(lines.Contains("Entertainment"));
            
            // New group should be added with ## prefix
            Assert.IsTrue(lines.Contains("##Movies"));
            
            // Should not duplicate existing groups
            var sportsCount = lines.Count(line => 
            {
                var trimmed = line.TrimStart().TrimStart('#').Trim();
                return trimmed.Equals("Sports", StringComparison.OrdinalIgnoreCase);
            });
            Assert.AreEqual(1, sportsCount);
            
            // Backup should exist
            var backupPath = $"{tmpFile}.bak";
            Assert.IsTrue(File.Exists(backupPath));
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlesEmptyLinesInExistingFile()
    {
        var tmpFile = Path.GetTempFileName();
        
        try
        {
            var existingContent = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 0.40 ######

#Sports

News

";
            await File.WriteAllTextAsync(tmpFile, existingContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 2"" group-title=""Movies"",Channel 2
http://example.com/2";

            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups, 
                new CommandOptionSet(new Dictionary<string, List<string>>()), 
                null, null, null, 
                new Dictionary<string, string>(), 
                "http://test", 
                null, null, 
                tmpFile, 
                null, null, 
                false, false);
            
            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            Assert.AreEqual(0, result);
            
            var lines = await File.ReadAllLinesAsync(tmpFile);
            
            // Should preserve blank lines from original
            Assert.IsTrue(lines.Any(line => string.IsNullOrWhiteSpace(line)));
            
            // Should add new group with ## prefix
            Assert.IsTrue(lines.Contains("##Movies"));
            
            // Cleanup backup
            var backupPath = $"{tmpFile}.bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NoNewGroups_WhenAllExist()
    {
        var tmpFile = Path.GetTempFileName();
        
        try
        {
            var existingContent = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 0.40 ######

#Sports
News";
            await File.WriteAllTextAsync(tmpFile, existingContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 2"" group-title=""News"",Channel 2
http://example.com/2";

            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups, 
                new CommandOptionSet(new Dictionary<string, List<string>>()), 
                null, null, null, 
                new Dictionary<string, string>(), 
                "http://test", 
                null, null, 
                tmpFile, 
                null, null, 
                false, false);
            
            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            Assert.AreEqual(0, result);
            
            // Backup should have been deleted since nothing changed
            var backupPath = $"{tmpFile}.bak";
            Assert.IsFalse(File.Exists(backupPath));
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CaseInsensitiveGroupMatching()
    {
        var tmpFile = Path.GetTempFileName();
        
        try
        {
            var existingContent = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 0.40 ######

#SPORTS";
            await File.WriteAllTextAsync(tmpFile, existingContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""sports"",Channel 1
http://example.com/1
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 2"" group-title=""Sports"",Channel 2
http://example.com/2";

            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups, 
                new CommandOptionSet(new Dictionary<string, List<string>>()), 
                null, null, null, 
                new Dictionary<string, string>(), 
                "http://test", 
                null, null, 
                tmpFile, 
                null, null, 
                false, false);
            
            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            Assert.AreEqual(0, result);
            
            var lines = await File.ReadAllLinesAsync(tmpFile);
            
            // Should not duplicate "sports" in different cases
            var sportsVariants = lines.Count(line => 
            {
                var trimmed = line.TrimStart().TrimStart('#').Trim();
                return trimmed.Equals("sports", StringComparison.OrdinalIgnoreCase);
            });
            Assert.AreEqual(1, sportsVariants);
            
            // Cleanup backup since no new groups were added
            var backupPath = $"{tmpFile}.bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsInvalidFile()
    {
        var tmpFile = Path.GetTempFileName();
        
        try
        {
            // Create a file that doesn't have the proper header
            var invalidContent = @"This is not a groups file
Just some random content
Sports
News";
            await File.WriteAllTextAsync(tmpFile, invalidContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1";

            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups, 
                new CommandOptionSet(new Dictionary<string, List<string>>()), 
                null, null, null, 
                new Dictionary<string, string>(), 
                "http://test", 
                null, null, 
                tmpFile, 
                null, null, 
                false, false);
            
            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            // Should return error code
            Assert.AreNotEqual(0, result);
            
            // File should NOT have been modified (still has invalid content)
            var fileContent = await File.ReadAllTextAsync(tmpFile);
            Assert.IsTrue(fileContent.Contains("This is not a groups file"));
            
            // No backup should have been created since we didn't modify
            var backupPath = $"{tmpFile}.bak";
            Assert.IsFalse(File.Exists(backupPath));
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsMajorVersionMismatch()
    {
        var tmpFile = Path.GetTempFileName();
        
        try
        {
            // Create a file with a different major version
            var oldVersionContent = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 99.0 ######

#Sports";
            await File.WriteAllTextAsync(tmpFile, oldVersionContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1";

            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups, 
                new CommandOptionSet(new Dictionary<string, List<string>>()), 
                null, null, null, 
                new Dictionary<string, string>(), 
                "http://test", 
                null, null, 
                tmpFile, 
                null, null, 
                false, false);
            
            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            // Should return error code
            Assert.AreNotEqual(0, result);
            
            // File should NOT have been modified (still has version 99.0)
            var fileContent = await File.ReadAllTextAsync(tmpFile);
            Assert.IsTrue(fileContent.Contains("version 99.0"));
            
            // No backup should have been created since we didn't modify
            var backupPath = $"{tmpFile}.bak";
            Assert.IsFalse(File.Exists(backupPath));
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ForceFlag_AllowsInvalidFile()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            // Create a file that doesn't have the proper header
            var invalidContent = @"This is not a groups file
Just some random content
Sports
News";
            await File.WriteAllTextAsync(tmpFile, invalidContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 2"" group-title=""Movies"",Channel 2
http://example.com/2";

            var options = new Dictionary<string, List<string>> { { "force", new List<string> { CommandOptionParser.FlagPresentValue } } };
            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups,
                new CommandOptionSet(options),
                null, null, null,
                new Dictionary<string, string>(),
                "http://test",
                null, null,
                tmpFile,
                null, null,
                false, false);

            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            // Should succeed with --force
            Assert.AreEqual(0, result);
            
            // File should be modified with new groups
            var lines = await File.ReadAllLinesAsync(tmpFile);
            Assert.IsTrue(lines.Any(l => l.StartsWith("##Movies")));
            
            // Backup should have been created
            var backupPath = $"{tmpFile}.bak";
            Assert.IsTrue(File.Exists(backupPath));
            
            // Cleanup
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ForceFlag_AllowsVersionMismatch()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            // Create a file with a different major version
            var oldVersionContent = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 99.0 ######

#Sports";
            await File.WriteAllTextAsync(tmpFile, oldVersionContent);

            var playlistContent = @"#EXTM3U
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 1"" group-title=""Sports"",Channel 1
http://example.com/1
#EXTINF:-1 tvg-id="""" tvg-name=""Channel 2"" group-title=""Movies"",Channel 2
http://example.com/2";

            var options = new Dictionary<string, List<string>> { { "force", new List<string> { CommandOptionParser.FlagPresentValue } } };
            var cmd = CreateGroupsCommand(playlistContent);
            var context = new CommandContext(
                CommandKind.Groups,
                new CommandOptionSet(options),
                null, null, null,
                new Dictionary<string, string>(),
                "http://test",
                null, null,
                tmpFile,
                null, null,
                false, false);

            var result = await cmd.ExecuteAsync(context, CancellationToken.None);
            
            // Should succeed with --force
            Assert.AreEqual(0, result);
            
            // File should be modified with new groups
            var lines = await File.ReadAllLinesAsync(tmpFile);
            Assert.IsTrue(lines.Any(l => l.StartsWith("##Movies")));
            
            // Backup should have been created
            var backupPath = $"{tmpFile}.bak";
            Assert.IsTrue(File.Exists(backupPath));
            
            // Cleanup
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    private static GroupsCommand CreateGroupsCommand(string playlistResponse)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var diagnostics = new StringWriter();
        var httpClient = new HttpClient(new MockHttpMessageHandler(playlistResponse));
        var parser = new PlaylistParser();
        
        return new GroupsCommand(stdout, stderr, diagnostics, httpClient, parser);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        public MockHttpMessageHandler(string response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(_response) });
    }
}
