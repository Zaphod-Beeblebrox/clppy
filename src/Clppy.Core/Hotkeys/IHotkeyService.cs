using System;
using Clppy.Core.Models;

namespace Clppy.Core.Hotkeys;

public interface IHotkeyService : IDisposable
{
    event Action<Guid>? HotkeyTriggered;
    bool RegisterHotkey(HotkeyRegistration reg);
    void UnregisterHotkey(Guid clipId);
    void UnregisterAll();
    bool IsHotkeyAvailable(string modifierKeys, char key);
}
