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
            Title = "Select Tradovate Matched Fills CSV",
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
        return new TradovateCsvFileSelection(Path.GetFileName(dialog.FileName), content);
    }
}
