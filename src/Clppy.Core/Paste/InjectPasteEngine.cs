using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Clppy.Core.Models;

namespace Clppy.Core.Paste;

public class InjectPasteEngine : IPasteEngine
{
    private const int INPUT_KEYBOARD = 0;
    private const int KEYEVENTF_KEYUP = 0x0002;
    private const int KEYEVENTF_UNICODE = 0x0004;
    private readonly int _keystrokeDelayMs;

    public InjectPasteEngine(int keystrokeDelayMs = 5)
    {
        _keystrokeDelayMs = keystrokeDelayMs;
    }

    public async Task PasteAsync(Clip clip, IntPtr targetWindow)
    {
        if (clip == null || string.IsNullOrEmpty(clip.PlainText))
            return;

        SetForegroundWindow(targetWindow);
        await Task.Delay(50);

        var keystrokes = BuildKeystrokeSequence(clip.PlainText);

        foreach (var keystroke in keystrokes)
        {
            SendKeystroke(keystroke);
            if (_keystrokeDelayMs > 0)
                await Task.Delay(_keystrokeDelayMs);
        }
    }

    public List<Keystroke> BuildKeystrokeSequence(string text)
    {
        var keystrokes = new List<Keystroke>();

        if (string.IsNullOrEmpty(text))
            return keystrokes;

        foreach (var c in text)
        {
            switch (c)
            {
                case '\t':
                    keystrokes.Add(new Keystroke
                    {
                        VirtualKeyCode = 0x09,
                        IsUnicode = false
                    });
                    break;
                case '\n':
                    keystrokes.Add(new Keystroke
                    {
                        VirtualKeyCode = 0x0D,
                        IsUnicode = false
                    });
                    break;
                case '\r':
                    break;
                default:
                    keystrokes.Add(new Keystroke
                    {
                        UnicodeChar = (ushort)c,
                        IsUnicode = true
                    });
                    break;
            }
        }

        return keystrokes;
    }

    private void SendKeystroke(Keystroke keystroke)
    {
        var input = new INPUT();
        input.type = INPUT_KEYBOARD;

        if (keystroke.IsUnicode)
        {
            input.u = new INPUT_UNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = keystroke.UnicodeChar,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };
            SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));
        }
        else
        {
            input.u = new INPUT_UNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)keystroke.VirtualKeyCode,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };
            SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));

            input.u.ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public INPUT_UNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_UNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
