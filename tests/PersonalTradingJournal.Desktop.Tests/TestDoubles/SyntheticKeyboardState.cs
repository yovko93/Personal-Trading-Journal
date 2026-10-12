using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>
/// Defines modifiers for one synchronous synthetic event. SetKeyboardState affects only
/// this thread's input table, not physical input or other apps. Never pump inside action.
/// </summary>
internal static class SyntheticKeyboardState
{
    internal static void WithShift(bool down, Action action)
    {
        var previous = new byte[256];
        if (!GetKeyboardState(previous)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var state = (byte[])previous.Clone();
        foreach (int key in new[] { 0x10, 0xA0, 0xA1 }) state[key] = down ? (byte)0x80 : (byte)0;
        if (!SetKeyboardState(state)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            Assert.Equal(down, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            action();
        }
        finally
        {
            if (!SetKeyboardState(previous)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState([Out] byte[] state);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKeyboardState(byte[] state);
}
