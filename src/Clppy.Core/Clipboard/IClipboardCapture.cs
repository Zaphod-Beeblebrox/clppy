using Clppy.Core.Models;

namespace Clppy.Core.Clipboard;

public interface IClipboardCapture
{
    event Action<Clip>? ClipCaptured;
    void StartListening();
    void StopListening();
}