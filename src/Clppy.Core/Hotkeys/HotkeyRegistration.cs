using System;

namespace Clppy.Core.Hotkeys;

public class HotkeyRegistration
{
    public Guid ClipId { get; set; }
    public string ModifierKeys { get; set; } = string.Empty;
    public char Key { get; set; }
    public int Id { get; set; }
}
