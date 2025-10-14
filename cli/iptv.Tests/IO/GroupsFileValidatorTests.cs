using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Iptv.Cli;
using Iptv.Cli.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Iptv.Tests.IO;

[TestClass]
public class GroupsFileValidatorTests
{
    [TestMethod]
    public async Task ValidateFileAsync_NonExistentFile_ReturnsValid()
    {
        var result = await GroupsFileValidator.ValidateFileAsync("nonexistent.txt", CancellationToken.None);
        
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.FileVersion);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateFileAsync_ValidFile_ReturnsValid()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var content = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 1.0 ######

#Sports
News";
            await File.WriteAllTextAsync(tmpFile, content);

            var result = await GroupsFileValidator.ValidateFileAsync(tmpFile, CancellationToken.None);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("1.0", result.FileVersion);
            Assert.IsNull(result.ErrorMessage);
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
    public async Task ValidateFileAsync_InvalidFile_ReturnsInvalid()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var content = @"This is not a groups file
Just some random content";
            await File.WriteAllTextAsync(tmpFile, content);

            var result = await GroupsFileValidator.ValidateFileAsync(tmpFile, CancellationToken.None);

            Assert.IsFalse(result.IsValid);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("missing header"));
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
    public async Task ValidateFileAsync_MajorVersionMismatch_ReturnsInvalid()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var content = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP. ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.              ######
######  Created with iptv version 99.0 ######

#Sports";
            await File.WriteAllTextAsync(tmpFile, content);

            var result = await GroupsFileValidator.ValidateFileAsync(tmpFile, CancellationToken.None);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("99.0", result.FileVersion);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("major version mismatch"));
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
    public async Task ValidateFileAsync_MinorVersionDifferent_ReturnsValid()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var currentVersion = GroupsFileValidator.GetCurrentVersion();
            var parts = currentVersion.Split('.');
            var differentMinor = int.Parse(parts[1]) + 1;
            var testVersion = $"{parts[0]}.{differentMinor}";

            var content = $@"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version {testVersion} ######

#Sports";
            await File.WriteAllTextAsync(tmpFile, content);

            var result = await GroupsFileValidator.ValidateFileAsync(tmpFile, CancellationToken.None);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(testVersion, result.FileVersion);
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
    public async Task ValidateFileAsync_NoVersion_ReturnsInvalid()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var content = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######

#Sports
News";
            await File.WriteAllTextAsync(tmpFile, content);

            var result = await GroupsFileValidator.ValidateFileAsync(tmpFile, CancellationToken.None);

            Assert.IsFalse(result.IsValid);
            Assert.IsNull(result.FileVersion);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("does not contain version information"));
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
    public async Task ValidateFileAsync_OldHeaderFormat_ReturnsValid()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            // Old format without the extra padding
            var content = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP. ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.              ######
######  Created with iptv version 0.40 ######

#Sports
News";
            await File.WriteAllTextAsync(tmpFile, content);

            var result = await GroupsFileValidator.ValidateFileAsync(tmpFile, CancellationToken.None);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("0.40", result.FileVersion);
            Assert.IsNull(result.ErrorMessage);
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
    public async Task ValidateFileAsync_NewHeaderFormat_ReturnsValid()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            // New format with proper padding and alignment
            var content = @"######  This is a DROP list. Put a '#' in front of any group you want to KEEP.  ######
######  Lines without '#' will be DROPPED. Blank lines are ignored.             ######
######  Created with iptv version 0.40                                          ######

#Sports
News";
            await File.WriteAllTextAsync(tmpFile, content);

            var result = await GroupsFileValidator.ValidateFileAsync(tmpFile, CancellationToken.None);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual("0.40", result.FileVersion);
            Assert.IsNull(result.ErrorMessage);
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
    public void CreateBackupPath_NoExistingBackup_ReturnsBasicBackup()
    {
        var originalPath = Path.Combine(Path.GetTempPath(), "test.txt");
        var expectedBackup = Path.Combine(Path.GetTempPath(), "test.txt.bak");

        var backupPath = GroupsFileValidator.CreateBackupPath(originalPath);

        Assert.AreEqual(expectedBackup, backupPath);
    }

    [TestMethod]
    public void CreateBackupPath_ExistingBackup_ReturnsNumberedBackup()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            var originalPath = Path.Combine(tmpDir, "test.txt");
            var bakPath = Path.Combine(tmpDir, "test.txt.bak");
            
            // Create existing backup
            File.WriteAllText(bakPath, "existing backup");

            var backupPath = GroupsFileValidator.CreateBackupPath(originalPath);
            var expectedBackup = Path.Combine(tmpDir, "test.txt.bak1");

            Assert.AreEqual(expectedBackup, backupPath);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }
        }
    }

    [TestMethod]
    public async Task CreateBackupAsync_CreatesBackupFile()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            var content = "Test content for backup";
            await File.WriteAllTextAsync(tmpFile, content);

            var backupPath = GroupsFileValidator.CreateBackupPath(tmpFile);
            await GroupsFileValidator.CreateBackupAsync(tmpFile, CancellationToken.None);

            Assert.IsTrue(File.Exists(backupPath));
            var backupContent = await File.ReadAllTextAsync(backupPath);
            Assert.AreEqual(content, backupContent);

            // Cleanup
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
    public void CreateHeader_ContainsRequiredLines()
    {
        var header = GroupsFileValidator.CreateHeader();

        Assert.IsTrue(header.Length >= 4);
        Assert.IsTrue(header[0].Contains("DROP list"));
        Assert.IsTrue(header[1].Contains("DROPPED"));
        Assert.IsTrue(header[2].Contains("New groups are marked"));
        Assert.IsTrue(header[3].Contains("Created with iptv version"));
        
        // Verify all header lines end with " ######" at the same position (88 chars total)
        Assert.AreEqual(88, header[0].Length);
        Assert.AreEqual(88, header[1].Length);
        Assert.AreEqual(88, header[2].Length);
        Assert.AreEqual(88, header[3].Length);
        Assert.IsTrue(header[0].EndsWith(" ######"));
        Assert.IsTrue(header[1].EndsWith(" ######"));
        Assert.IsTrue(header[2].EndsWith(" ######"));
        Assert.IsTrue(header[3].EndsWith(" ######"));
    }

    [TestMethod]
    public void CreateHeader_ContainsCurrentVersion()
    {
        var header = GroupsFileValidator.CreateHeader();
        var currentVersion = GroupsFileValidator.GetCurrentVersion();

        var versionLine = Array.Find(header, l => l.Contains("Created with iptv version"));
        Assert.IsNotNull(versionLine);
        Assert.IsTrue(versionLine.Contains(currentVersion));
    }

    [TestMethod]
    public void GetCurrentVersion_ReturnsValidVersion()
    {
        var version = GroupsFileValidator.GetCurrentVersion();

        Assert.IsFalse(string.IsNullOrEmpty(version));
        Assert.IsTrue(version.Contains("."));
        
        var parts = version.Split('.');
        Assert.AreEqual(2, parts.Length);
        Assert.IsTrue(int.TryParse(parts[0], out _));
        Assert.IsTrue(int.TryParse(parts[1], out _));
    }
}
