using Microsoft.Win32;
using System.IO;

namespace PersonalTradingJournal.Desktop.Imports;

public sealed class WpfTradovateCsvFilePicker : ITradovateCsvFilePicker
{
    public TradovateCsvFileSelection? Pick()
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "CSV files (*.csv)|*.csv",
            Multiselect = false,
            Title = "Select Topstep or Tradovate CSV",
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var content = new FileStream(
            dialog.FileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        string path = dialog.FileName;
        return new TradovateCsvFileSelection(Path.GetFileName(path), content,
            () => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan));
    }
}
