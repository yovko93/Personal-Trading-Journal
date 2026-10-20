using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Screenshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookImageAdmissionTests
{
    [Fact]
    public async Task HostDecoderAndManagedImportReadBackExactOriginalThroughApplicationPort()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        string root = Directory.CreateTempSubdirectory("ptj-notebook-admission-").FullName;
        try
        {
            var paths = new LocalApplicationPaths(root); var codec = new NotebookJsonCodec();
            var factory = db.Provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            var files = new NotebookImageFiles(paths, factory, new WpfNotebookImageValidator());
            var notes = new NotebookService(factory, codec, imageFiles: files);
            INotebookImages images = new NotebookImageService(factory, notes, codec, files);
            var note = (await notes.ExecuteAsync(new CreateNotebookNote())).Note!; var id = Guid.NewGuid(); byte[] original = Encode(2, 3);
            using var input = new MemoryStream(original);
            var result = await images.ImportAsync(new(note.Id, note.Version, id,
                new([PersonalTradingJournal.Domain.Notebook.NotebookNode.Image(Guid.NewGuid(), id)])), input);
            Assert.Equal(NotebookWriteStatus.Success, result.Status);
            Assert.Equal(original, (await images.ReadOriginalAsync(note.Id, id)).Original);
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledDecoderFullyValidatesPngAndJpegWithoutChangingOriginal(bool jpeg)
    {
        byte[] bytes = Encode(2, 3, jpeg); var original = bytes.ToArray();
        var probe = new WpfNotebookImageValidator().Validate(bytes);
        Assert.Equal(new(NotebookImageStatus.Success, jpeg ? ".jpg" : ".png", 2, 3), probe);
        Assert.Equal(original, bytes);
    }
    [Theory]
    [InlineData(8193, 1)]
    [InlineData(1, 8193)]
    [InlineData(4001, 4000)]
    public void DecoderRejectsDimensionAndTotalPixelBoundsBeforeMaterialization(int width, int height)
        => Assert.Equal(NotebookImageStatus.TooLarge, new WpfNotebookImageValidator().Validate(Encode(width, height)).Status);
    [Fact]
    public void WrongSignatureCorruptPayloadAndAnimatedContainerAreRejected()
    {
        var validator = new WpfNotebookImageValidator();
        Assert.Equal(NotebookImageStatus.Unsupported, validator.Validate("GIF89a"u8.ToArray()).Status);
        byte[] corrupt = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
        Assert.NotEqual(NotebookImageStatus.Success, validator.Validate(corrupt).Status);
        byte[] animated = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 8, .. "acTL"u8.ToArray(), 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.Equal(NotebookImageStatus.Unsupported, validator.Validate(animated).Status);
        Assert.Equal(NotebookImageStatus.Unsupported, new UnavailableNotebookImageValidator().Validate(Encode(1, 1)).Status);
    }
    private static byte[] Encode(int width, int height, bool jpeg = false)
    {
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, new byte[checked(width * height)], width);
        BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder { QualityLevel = 93 } : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source)); using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }
}
