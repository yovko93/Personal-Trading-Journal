using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PersonalTradingJournal.TestProcessProbe;

// Built with the test suite, never compiled or run through a scripting host at
// test time. No WPF, journal services, packages, or customer data are involved.
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (args is ["output", "0" or "7"])
        {
            WriteOutput(Console.Out, "stdout", 'O');
            WriteOutput(Console.Error, "stderr", 'E');
            return int.Parse(args[1], CultureInfo.InvariantCulture);
        }
        if (args is ["blocked-sta"]) return BlockForegroundSta();
        if (args is ["blocked-child", var parentId]) return WaitForParent(int.Parse(parentId, CultureInfo.InvariantCulture));
        Console.Error.WriteLine("Unknown synthetic probe mode.");
        return 2;
    }

    private static void WriteOutput(TextWriter writer, string name, char padding)
    {
        writer.Write($"probe {name}\n");
        // Exceeds both OS pipe buffers and the supervisor's 128-KiB returned
        // tail. Complete output must survive in the streamed artifact files.
        for (int line = 0; line < 2048; line++)
            writer.Write(string.Create(CultureInfo.InvariantCulture, $"{name} {line:D4}: {new string(padding, 96)}\n"));
        writer.Write($"complete {name} Ω\n");
        writer.Flush();
    }

    private static int BlockForegroundSta()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };
        start.ArgumentList.Add("blocked-child");
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("Probe child did not start.");
        try
        {
            // The marker proves both the descendant and the foreground STA
            // reached the intended phase, rather than testing a startup hang.
            string? ready = child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            if (ready != "CHILD READY") throw new InvalidOperationException("Probe child did not become ready.");
            var thread = new Thread(() =>
            {
                Console.WriteLine("PARENT " + Environment.ProcessId);
                Console.WriteLine("CHILD " + child.Id);
                Console.WriteLine("FOREGROUND " + Thread.CurrentThread.GetApartmentState());
                Console.Out.Flush();
                using var blocked = new ManualResetEventSlim(false);
                blocked.Wait();
            })
            {
                IsBackground = false,
                Name = "synthetic-blocked-foreground-sta"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return 0;
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            child.WaitForExit();
        }
    }

    private static int WaitForParent(int parentId)
    {
        try
        {
            // Backstop the immediate-log-failure race, but do not mask a broken
            // tree kill: ready descendants must remain alive beyond the tests'
            // five-second cleanup assertions if only their parent is killed.
            using Process parent = Process.GetProcessById(parentId);
            Console.WriteLine("CHILD READY");
            Console.Out.Flush();
            parent.WaitForExit();
            Thread.Sleep(TimeSpan.FromSeconds(30));
        }
        catch (ArgumentException) { } // The parent was already terminated.
        return 0;
    }
}
