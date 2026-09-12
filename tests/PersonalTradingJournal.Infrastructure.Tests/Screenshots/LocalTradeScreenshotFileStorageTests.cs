using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Screenshots;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Screenshots;

public sealed class LocalTradeScreenshotFileStorageTests
{
    [Fact]
    public async Task StoreAsyncWritesExactBytesAndReturnsOpaqueKey()
    {
        using var fixture = new StorageFixture();
        byte[] expected = [0, 1, 2, 127, 128, 254, 255];
        await using var content = new MemoryStream(expected);

        string storageKey = await fixture.Storage.StoreAsync(content, ".png");

        Assert.EndsWith(".png", storageKey, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(storageKey), "N", out _));
        Assert.Equal(
            expected,
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.Paths.ScreenshotsDirectory, storageKey)));
    }

    [Theory]
    [InlineData("png", ".png")]
    [InlineData(".PNG", ".png")]
    [InlineData("jpg", ".jpg")]
    [InlineData(".JPG", ".jpg")]
    [InlineData("jpeg", ".jpeg")]
    [InlineData(".JPEG", ".jpeg")]
    [InlineData("webp", ".webp")]
    [InlineData(".WEBP", ".webp")]
    public async Task StoreAsyncNormalizesSupportedExtension(
        string extension,
        string expectedExtension)
    {
        using var fixture = new StorageFixture();
        await using var content = new MemoryStream([42]);

        string storageKey = await fixture.Storage.StoreAsync(content, extension);

        Assert.Equal(expectedExtension, Path.GetExtension(storageKey));
    }

    [Fact]
    public async Task StoreAsyncGeneratesDifferentKeysForDuplicateContent()
    {
        using var fixture = new StorageFixture();
        byte[] bytes = [1, 2, 3];
        await using var firstContent = new MemoryStream(bytes);
        await using var secondContent = new MemoryStream(bytes);

        string firstKey = await fixture.Storage.StoreAsync(firstContent, ".jpg");
        string secondKey = await fixture.Storage.StoreAsync(secondContent, ".jpg");

        Assert.NotEqual(firstKey, secondKey);
        Assert.Equal(2, Directory.GetFiles(fixture.Paths.ScreenshotsDirectory).Length);
    }

    [Fact]
    public async Task StoreAsyncSupportsNonSeekableReadableStream()
    {
        using var fixture = new StorageFixture();
        byte[] expected = [5, 10, 15, 20];
        await using var content = new NonSeekableReadStream(expected);

        string storageKey = await fixture.Storage.StoreAsync(content, ".webp");

        Assert.Equal(
            expected,
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.Paths.ScreenshotsDirectory, storageKey)));
    }

    [Fact]
    public async Task StoreAsyncCopiesFromCurrentStreamPosition()
    {
        using var fixture = new StorageFixture();
        await using var content = new MemoryStream([1, 2, 3, 4]);
        content.Position = 2;

        string storageKey = await fixture.Storage.StoreAsync(content, ".png");

        Assert.Equal(
            [3, 4],
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.Paths.ScreenshotsDirectory, storageKey)));
    }

    [Fact]
    public async Task StoreAsyncAllowsZeroByteContent()
    {
        using var fixture = new StorageFixture();
        await using var content = new MemoryStream();

        string storageKey = await fixture.Storage.StoreAsync(content, ".png");

        Assert.Empty(
            await File.ReadAllBytesAsync(
                Path.Combine(fixture.Paths.ScreenshotsDirectory, storageKey)));
    }

    [Fact]
    public async Task OpenReadAsyncReturnsExactStoredBytes()
    {
        using var fixture = new StorageFixture();
        byte[] expected = [9, 8, 7, 6];
        await using var content = new MemoryStream(expected);
        string storageKey = await fixture.Storage.StoreAsync(content, ".jpeg");

        await using Stream opened = Assert.IsAssignableFrom<Stream>(
            await fixture.Storage.OpenReadAsync(storageKey));
        using var copy = new MemoryStream();
        await opened.CopyToAsync(copy);

        Assert.Equal(expected, copy.ToArray());
        Assert.True(opened.CanRead);
    }

    [Fact]
    public async Task OpenReadAsyncReturnsNullForMissingValidKey()
    {
        using var fixture = new StorageFixture();

        Stream? result = await fixture.Storage.OpenReadAsync($"{Guid.NewGuid():N}.png");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteIfExistsAsyncRemovesExistingFile()
    {
        using var fixture = new StorageFixture();
        await using var content = new MemoryStream([1]);
        string storageKey = await fixture.Storage.StoreAsync(content, ".png");
        string filePath = Path.Combine(fixture.Paths.ScreenshotsDirectory, storageKey);

        await fixture.Storage.DeleteIfExistsAsync(storageKey);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteIfExistsAsyncIsNoOpForMissingValidKey()
    {
        using var fixture = new StorageFixture();

        await fixture.Storage.DeleteIfExistsAsync($"{Guid.NewGuid():N}.png");

        Assert.False(Directory.Exists(fixture.Paths.ScreenshotsDirectory));
    }

    [Fact]
    public async Task OpenAndDeleteHonorPreCancelledToken()
    {
        using var fixture = new StorageFixture();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        string storageKey = $"{Guid.NewGuid():N}.png";

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Storage.OpenReadAsync(storageKey, cancellationSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Storage.DeleteIfExistsAsync(storageKey, cancellationSource.Token));

        Assert.False(Directory.Exists(fixture.Paths.ScreenshotsDirectory));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("..\\outside.png")]
    [InlineData("nested/image.png")]
    [InlineData("nested\\image.png")]
    [InlineData("C:\\temp\\image.png")]
    [InlineData("\\\\server\\share\\image.png")]
    [InlineData("/rooted.png")]
    [InlineData(" image.png")]
    [InlineData("image.png ")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task OpenAndDeleteRejectUnsafeStorageKey(string storageKey)
    {
        using var fixture = new StorageFixture();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Storage.OpenReadAsync(storageKey));
        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Storage.DeleteIfExistsAsync(storageKey));

        Assert.False(Directory.Exists(fixture.Paths.ScreenshotsDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".gif")]
    [InlineData("bmp")]
    [InlineData(" .png")]
    [InlineData(".png ")]
    public async Task StoreAsyncRejectsInvalidExtension(string extension)
    {
        using var fixture = new StorageFixture();
        await using var content = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Storage.StoreAsync(content, extension));

        Assert.False(Directory.Exists(fixture.Paths.ScreenshotsDirectory));
    }

    [Fact]
    public async Task StoreAsyncRejectsNullOrUnreadableContent()
    {
        using var fixture = new StorageFixture();
        await using var unreadableContent = new WriteOnlyStream();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.Storage.StoreAsync(null!, ".png"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Storage.StoreAsync(unreadableContent, ".png"));

        Assert.False(Directory.Exists(fixture.Paths.ScreenshotsDirectory));
    }

    [Fact]
    public async Task CancelledStoreAsyncLeavesNoFinalOrTemporaryFile()
    {
        using var fixture = new StorageFixture();
        using var cancellationSource = new CancellationTokenSource();
        await using var content = new CancellingReadStream(cancellationSource);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Storage.StoreAsync(
                content,
                ".png",
                cancellationSource.Token));

        Assert.Empty(Directory.GetFiles(fixture.Paths.ScreenshotsDirectory));
    }

    [Fact]
    public async Task FailedStoreAsyncLeavesNoFinalOrTemporaryFile()
    {
        using var fixture = new StorageFixture();
        await using var content = new ThrowingReadStream();

        await Assert.ThrowsAsync<IOException>(
            () => fixture.Storage.StoreAsync(content, ".png"));

        Assert.Empty(Directory.GetFiles(fixture.Paths.ScreenshotsDirectory));
    }

    [Fact]
    public void AddPersistenceRegistersFileStorageAsSingletonWithoutCreatingDirectory()
    {
        using var fixture = new StorageFixture();
        var services = new ServiceCollection();
        services.AddPersistence(fixture.Paths);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ITradeScreenshotFileStorage first =
            serviceProvider.GetRequiredService<ITradeScreenshotFileStorage>();
        ITradeScreenshotFileStorage second =
            serviceProvider.GetRequiredService<ITradeScreenshotFileStorage>();

        Assert.IsType<LocalTradeScreenshotFileStorage>(first);
        Assert.Same(first, second);
        Assert.False(Directory.Exists(fixture.Paths.ScreenshotsDirectory));
    }

    private sealed class StorageFixture : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(LocalTradeScreenshotFileStorageTests),
            Guid.NewGuid().ToString("N"));

        public StorageFixture()
        {
            Paths = new LocalApplicationPaths(_rootDirectory);
            Storage = new LocalTradeScreenshotFileStorage(Paths);
        }

        public LocalApplicationPaths Paths { get; }

        public LocalTradeScreenshotFileStorage Storage { get; }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content, writable: false);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }

    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;
    }

    private sealed class CancellingReadStream(CancellationTokenSource cancellationSource)
        : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationSource.Cancel();
            return Task.FromCanceled<int>(cancellationToken);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationSource.Cancel();
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Deterministic read failure.");

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            Task.FromException<int>(new IOException("Deterministic read failure."));

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Deterministic read failure."));

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
