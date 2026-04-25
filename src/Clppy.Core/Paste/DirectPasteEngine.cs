using System;
using System.Threading.Tasks;
using Clppy.Core.Models;

namespace Clppy.Core.Paste;

public class DirectPasteEngine : IPasteEngine
{
    public async Task PasteAsync(Clip clip, IntPtr targetWindow)
    {
        // This P/Invoke will compile on Linux but won't execute
        // In a real implementation, this would:
        // 1. Set system clipboard to all formats from clip
        // 2. Send Ctrl+V via SendInput
        
        await Task.CompletedTask;
    }
}
