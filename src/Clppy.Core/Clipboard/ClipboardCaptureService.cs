using System;
using System.Runtime.InteropServices;
using System.Text;
using Clppy.Core.Models;
using Clppy.Core.Persistence;

namespace Clppy.Core.Clipboard;

public class ClipboardCaptureService : IClipboardCapture, IDisposable
{
    private readonly IClipRepository _clipRepository;
    private readonly HistoryBuffer _historyBuffer;
    private IntPtr _hwndListener;
    private bool _isListening;
    private bool _disposed;
    private DateTime _lastCaptureTime;
    private const int CAPTURE_COOLDOWN_MS = 500;
    private static ClipboardCaptureService? _currentInstance;

    public event Action<Clip>? ClipCaptured;

    public ClipboardCaptureService(IClipRepository clipRepository)
    {
        _clipRepository = clipRepository;
        _historyBuffer = new HistoryBuffer(20);
        _currentInstance = this;
    }

    public void StartListening()
    {
        if (_isListening) return;

        _hwndListener = CreateHiddenWindow();
        if (_hwndListener != IntPtr.Zero)
        {
            AddClipboardFormatListener(_hwndListener);
            _isListening = true;
        }
    }

    public void StopListening()
    {
        if (!_isListening) return;

        if (_hwndListener != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(_hwndListener);
            DestroyWindow(_hwndListener);
            _hwndListener = IntPtr.Zero;
        }
        _isListening = false;
    }

    private IntPtr CreateHiddenWindow()
    {
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = IntPtr.Zero,
            lpszClassName = "ClppyClipboardListener",
            hCursor = IntPtr.Zero,
            hIcon = IntPtr.Zero,
            hIconSm = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            cbClsExtra = 0,
            cbWndExtra = 0
        };

        var atom = RegisterClassEx(ref wc);
        if (atom == 0) return IntPtr.Zero;

        var hwnd = CreateWindowEx(0, "ClppyClipboardListener", "", 0,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        return hwnd;
    }

    private void CaptureClipboardContent()
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
                return;

            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Pinned = false,
                Method = PasteMethod.Direct
            };

            uint format = 0;
            while ((format = EnumClipboardFormats(format)) != 0)
            {
                if (format == CF_UNICODETEXT)
                {
                    var text = GetClipboardText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        clip.PlainText = text;
                        clip.Label = GetLabelFromText(text);
                    }
                }
                else if (format == CF_OEMTEXT)
                {
                    if (clip.PlainText == null)
                    {
                        var oemText = GetClipboardOemText();
                        if (!string.IsNullOrEmpty(oemText))
                        {
                            clip.PlainText = oemText;
                            clip.Label = GetLabelFromText(oemText);
                        }
                    }
                }
                else if (format == CF_RTF)
                {
                    var rtf = GetClipboardRtf();
                    if (rtf != null && rtf.Length > 0)
                        clip.Rtf = rtf;
                }
            }

            CloseClipboard();

            if (!string.IsNullOrEmpty(clip.PlainText) || clip.Rtf != null || clip.Html != null)
            {
                _ = _clipRepository.AddAsync(clip);
                _historyBuffer.Add(clip);
                ClipCaptured?.Invoke(clip);
            }
        }
        catch
        {
            try { CloseClipboard(); } catch { }
        }
    }

    private string? GetClipboardText()
    {
        var handle = GetClipboardData(CF_UNICODETEXT);
        if (handle == IntPtr.Zero) return null;

        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero) return null;

        try
        {
            return Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private string? GetClipboardOemText()
    {
        var handle = GetClipboardData(CF_OEMTEXT);
        if (handle == IntPtr.Zero) return null;

        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero) return null;

        try
        {
            return Marshal.PtrToStringAnsi(ptr);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private byte[]? GetClipboardRtf()
    {
        var rtfFormat = RegisterClipboardFormat("Rich Text Format");
        if (rtfFormat == 0) return null;

        var handle = GetClipboardData(rtfFormat);
        if (handle == IntPtr.Zero) return null;

        var ptr = GlobalLock(handle);
        if (ptr == IntPtr.Zero) return null;

        try
        {
            var size = GlobalSize(handle);
            var buffer = new byte[size];
            Marshal.Copy(ptr, buffer, 0, (int)size);
            return buffer;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private string GetLabelFromText(string text)
    {
        var firstLine = text.Split('\n')[0].Replace("\r", "");
        return firstLine.Length > 30 ? firstLine[..30] + "..." : firstLine;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopListening();
            _currentInstance = null;
            _disposed = true;
        }
    }

    private const int CF_UNICODETEXT = 13;
    private const int CF_OEMTEXT = 7;
    private const int CF_RTF = 0x00CF;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private readonly WndProcDelegate _wndProcDelegate = WndProc;

    private static IntPtr WndProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_CLIPBOARDUPDATE = 0x031D;
        
        if (Msg == WM_CLIPBOARDUPDATE && _currentInstance != null)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _currentInstance._lastCaptureTime).TotalMilliseconds;
            
            if (elapsed > CAPTURE_COOLDOWN_MS)
            {
                _currentInstance._lastCaptureTime = now;
                _currentInstance.CaptureClipboardContent();
            }
        }
        
        return DefWindowProc(hWnd, Msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint EnumClipboardFormats(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GlobalSize(IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);
}
