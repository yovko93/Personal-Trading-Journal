using PersonalTradingJournal.Application.Notebook;

namespace PersonalTradingJournal.Application.Tests.Notebook;

public sealed class NotebookImageContractTests
{
    [Fact]
    public void HeaderPolicyAllowsOnlyPngJpegAndWebpAndFailsClosedWithoutHostDecoder()
    {
        Assert.Equal(".png", NotebookImageAdmission.Extension([137, 80, 78, 71, 13, 10, 26, 10]));
        Assert.Equal(".jpg", NotebookImageAdmission.Extension([255, 216, 255]));
        byte[] webp = [.. "RIFF"u8.ToArray(), 12, 0, 0, 0, .. "WEBP"u8.ToArray()];
        Assert.Equal(".webp", NotebookImageAdmission.Extension(webp));
        Assert.Null(NotebookImageAdmission.Extension("GIF89a"u8));
        Assert.Null(NotebookImageAdmission.Extension([]));
        Assert.Equal(NotebookImageStatus.Unsupported, new UnavailableNotebookImageValidator().Validate(webp).Status);
    }
    [Fact]
    public void AnimationChunksAreDetectedWithoutSearchingInsideCompressedPixelData()
    {
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        byte[] png = [.. signature, 0, 0, 0, 4, .. "IDAT"u8.ToArray(), .. "acTL"u8.ToArray(), 0, 0, 0, 0];
        Assert.False(NotebookImageAdmission.HasAnimation(png, ".png"));
        Assert.True(NotebookImageAdmission.HasAnimation([.. png, 0, 0, 0, 8, .. "acTL"u8.ToArray()], ".png"));
        byte[] webp = [.. "RIFF"u8.ToArray(), 20, 0, 0, 0, .. "WEBPVP8X"u8.ToArray(), 10, 0, 0, 0, 2];
        Assert.True(NotebookImageAdmission.HasAnimation(webp, ".webp"));
    }
}
