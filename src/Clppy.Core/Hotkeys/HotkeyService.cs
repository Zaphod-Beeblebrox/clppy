using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Clppy.Core.Models;
using Clppy.Core.Persistence;

namespace Clppy.Core.Hotkeys;

public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly IClipRepository _clipRepository;
    private readonly Dictionary<int, HotkeyRegistration> _registeredHotkeys;
    private IntPtr _windowHandle;
    private bool _disposed;

    public event Action<Guid>? HotkeyTriggered;

    private const int WM_HOTKEY = 0x0312;

    public HotkeyService(IClipRepository clipRepository)
    {
        _clipRepository = clipRepository;
        _registeredHotkeys = new Dictionary<int, HotkeyRegistration>();
    }

    public bool RegisterHotkey(HotkeyRegistration reg)
    {
        try
        {
            var modifiers = ParseModifiers(reg.ModifierKeys);
            var virtualKey = KeyToVirtualKey(reg.Key);
            
            // Store registration
            _registeredHotkeys[reg.Id] = reg;
            
            // In real Windows implementation:
            // return RegisterHotKey(_windowHandle, reg.Id, modifiers, virtualKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void UnregisterHotkey(Guid clipId)
    {
        var registration = _registeredHotkeys.Values.FirstOrDefault(r => r.ClipId == clipId);
        if (registration != null)
        {
            _registeredHotkeys.Remove(registration.Id);
            
            // In real Windows implementation:
            // UnregisterHotKey(_windowHandle, registration.Id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var reg in _registeredHotkeys.Values)
        {
            // In real Windows implementation:
            // UnregisterHotKey(_windowHandle, reg.Id);
        }
        _registeredHotkeys.Clear();
    }

    public bool IsHotkeyAvailable(string modifierKeys, char key)
    {
        // Check if this exact combination is already registered
        foreach (var reg in _registeredHotkeys.Values)
        {
            if (reg.ModifierKeys == modifierKeys && reg.Key == key)
            {
                return false;
            }
        }
        return true;
    }

    private ModifierKeys ParseModifiers(string modifierKeys)
    {
        var modifiers = ModifierKeys.None;
        var parts = modifierKeys.Split('+');
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim().ToLower();
            if (trimmed == "ctrl" || trimmed == "control")
                modifiers |= ModifierKeys.ModControl;
            else if (trimmed == "alt")
                modifiers |= ModifierKeys.ModAlt;
            else if (trimmed == "shift")
                modifiers |= ModifierKeys.ModShift;
            else if (trimmed == "win" || trimmed == "windows")
                modifiers |= ModifierKeys.ModWin;
        }
        
        return modifiers;
    }

    private ushort KeyToVirtualKey(char key)
    {
        // Simple mapping for common keys
        if (key >= 'A' && key <= 'Z')
            return (ushort)(key - 'A' + 0x41);
        if (key >= '0' && key <= '9')
            return (ushort)(key - '0' + 0x30);
        
        // Special keys
        return key switch
        {
            '	' => 0x09,  // Tab
            '\r' => 0x0D,  // Enter
            '\u001B' => 0x1B,  // Escape
            _ => (ushort)key
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            UnregisterAll();
            _disposed = true;
        }
    }
}

[Flags]
internal enum ModifierKeys : uint
{
    None = 0,
    ModAlt = 0x0001,
    ModControl = 0x0002,
    ModShift = 0x0004,
    ModWin = 0x0008
}
