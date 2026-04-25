using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Clppy.Core.Models;

namespace Clppy.Core.Paste;

public class DirectPasteEngine : IPasteEngine
{
    private const int CF_TEXT = 1;
    private const int CF_UNICODETEXT = 13;
    private const int KEYEVENTF_KEYUP = 0x0002;

    public async Task PasteAsync(Clip clip, IntPtr targetWindow)
    {
        if (clip == null)
            return;

        // Activate the target window
        SetForegroundWindow(targetWindow);
        await Task.Delay(50);

        // Set clipboard data
        SetClipboardData(clip);

        // Send Ctrl+V
        SendCtrlV();
    }

    private void SetClipboardData(Clip clip)
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
                return;

            try
            {
                EmptyClipboard();

                if (!string.IsNullOrEmpty(clip.PlainText))
                {
                    SetClipboardText(clip.PlainText);
                }

                if (clip.Rtf != null && clip.Rtf.Length > 0)
                {
                    SetClipboardRtf(clip.Rtf);
                }

                if (clip.Html != null && clip.Html.Length > 0)
                {
                    SetClipboardHtml(clip.Html);
                }

                if (clip.PngImage != null && clip.PngImage.Length > 0)
                {
                    SetClipboardImage(clip.PngImage);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch
        {
        }
    }

    private void SetClipboardText(string text)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        var globalHandle = GlobalAlloc(0x0002, (uint)(textBytes.Length + 1));
        if (globalHandle != IntPtr.Zero)
        {
            var lockedPtr = GlobalLock(globalHandle);
            if (lockedPtr != IntPtr.Zero)
            {
                Marshal.Copy(textBytes, 0, lockedPtr, textBytes.Length);
                Marshal.WriteByte(lockedPtr, textBytes.Length, 0);
                GlobalUnlock(globalHandle);
                SetClipboardData(CF_UNICODETEXT, globalHandle);
            }
            else
            {
                GlobalFree(globalHandle);
            }
        }
    }

    private void SetClipboardRtf(byte[] rtf)
    {
        var rtfFormat = RegisterClipboardFormat("Rich Text Format");
        var globalHandle = GlobalAlloc(0x0002, (uint)(rtf.Length + 1));
        if (globalHandle != IntPtr.Zero)
        {
            var lockedPtr = GlobalLock(globalHandle);
            if (lockedPtr != IntPtr.Zero)
            {
                Marshal.Copy(rtf, 0, lockedPtr, rtf.Length);
                Marshal.WriteByte(lockedPtr, rtf.Length, 0);
                GlobalUnlock(globalHandle);
                SetClipboardData(rtfFormat, globalHandle);
            }
            else
            {
                GlobalFree(globalHandle);
            }
        }
    }

    private void SetClipboardHtml(byte[] html)
    {
        var htmlFormat = RegisterClipboardFormat("HTML Format");
        var globalHandle = GlobalAlloc(0x0002, (uint)(html.Length + 1));
        if (globalHandle != IntPtr.Zero)
        {
            var lockedPtr = GlobalLock(globalHandle);
            if (lockedPtr != IntPtr.Zero)
            {
                Marshal.Copy(html, 0, lockedPtr, html.Length);
                Marshal.WriteByte(lockedPtr, html.Length, 0);
                GlobalUnlock(globalHandle);
                SetClipboardData(htmlFormat, globalHandle);
            }
            else
            {
                GlobalFree(globalHandle);
            }
        }
    }

    private void SetClipboardImage(byte[] pngData)
    {
        var bitmapFormat = RegisterClipboardFormat("PNG");
        var globalHandle = GlobalAlloc(0x0002, (uint)pngData.Length);
        if (globalHandle != IntPtr.Zero)
        {
            var lockedPtr = GlobalLock(globalHandle);
            if (lockedPtr != IntPtr.Zero)
            {
                Marshal.Copy(pngData, 0, lockedPtr, pngData.Length);
                GlobalUnlock(globalHandle);
                SetClipboardData(bitmapFormat, globalHandle);
            }
            else
            {
                GlobalFree(globalHandle);
            }
        }
    }

    private void SendCtrlV()
    {
        var input = new INPUT();
        input.type = 1;

        input.u = new INPUT_UNION
        {
            ki = new KEYBDINPUT
            {
                wVk = 0x11,
                dwFlags = 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };
        SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));

        input.u.ki.wVk = 0x56;
        SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));

        input.u.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));

        input.u.ki.wVk = 0x11;
        SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));
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
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, uint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
