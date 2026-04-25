using System;
using Clppy.Core.Models;

namespace Clppy.Core.Paste;

public class PasteRouter
{
    public IPasteEngine GetEngine(PasteMethod method)
    {
        return method switch
        {
            PasteMethod.Direct => new DirectPasteEngine(),
            PasteMethod.Inject => new InjectPasteEngine(),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }

    public IPasteEngine GetEngine(Clip clip, bool forceOpposite)
    {
        var method = forceOpposite ? 
            clip.Method == PasteMethod.Direct ? PasteMethod.Inject : PasteMethod.Direct : 
            clip.Method;
            
        return GetEngine(method);
    }
}
