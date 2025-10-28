using System.IO;
using System.Linq;
using IPTVGuideDog.Core.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IPTVGuideDog.Cli.Tests.IO;

[TestClass]
public sealed class GroupSelectionFileTests
{
    [TestMethod]
    public void LoadSelection_SeparatesKeepPendingAndAll()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, @"######  Header line ######
                                            #Sports
                                            News
                                            ##Documentary
                                            #Movies
                                            ");

            var selection = GroupSelectionFile.LoadSelection(tempFile);

            CollectionAssert.AreEquivalent(
                new[] { "Sports", "Movies" },
                selection.Keep.ToList());

            CollectionAssert.AreEquivalent(
                new[] { "Sports", "News", "Documentary", "Movies" },
                selection.All.ToList());

            CollectionAssert.AreEquivalent(
                new[] { "Documentary" },
                selection.PendingReview.ToList());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public void LoadKeepSet_IgnoresPendingGroups()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "#Sports\n##Documentary\n");

            var keep = GroupSelectionFile.LoadKeepSet(tempFile);

            CollectionAssert.AreEquivalent(new[] { "Sports" }, keep.ToList());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

