using System;
using System.Threading.Tasks;
using Clppy.Core.Models;

namespace Clppy.Core.Paste;

public interface IPasteEngine
{
    Task PasteAsync(Clip clip, IntPtr targetWindow);
}
