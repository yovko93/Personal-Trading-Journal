using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Storage;

public sealed class LocalApplicationPathsTests
{
    [Fact]
    public void ConstructorBuildsExpectedPaths()
    {
        string localApplicationDataDirectory = Path.Combine(Path.GetTempPath(), "local-app-data");

        var paths = new LocalApplicationPaths(localApplicationDataDirectory);

        string expectedDataDirectory = Path.Combine(
            localApplicationDataDirectory,
            "PersonalTradingJournal");

        Assert.Equal(expectedDataDirectory, paths.DataDirectory);
        Assert.Equal(Path.Combine(expectedDataDirectory, "journal.db"), paths.DatabasePath);
        Assert.Equal(Path.Combine(expectedDataDirectory, "screenshots"), paths.ScreenshotsDirectory);
        Assert.Equal(Path.Combine(expectedDataDirectory, "logs"), paths.LogsDirectory);
        Assert.Equal(Path.Combine(expectedDataDirectory, "backups"), paths.BackupsDirectory);
    }

    [Fact]
    public void EnsureDirectoriesExistCreatesDirectoriesWithoutCreatingDatabase()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(LocalApplicationPathsTests),
            Guid.NewGuid().ToString("N"));
        var paths = new LocalApplicationPaths(testDirectory);

        try
        {
            Assert.False(Directory.Exists(paths.DataDirectory));

            paths.EnsureDirectoriesExist();

            Assert.True(Directory.Exists(paths.DataDirectory));
            Assert.True(Directory.Exists(paths.ScreenshotsDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
            Assert.True(Directory.Exists(paths.BackupsDirectory));
            Assert.False(File.Exists(paths.DatabasePath));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}
