using System;
using System.ComponentModel.DataAnnotations;

namespace Clppy.Core.Models;

public class Clip
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public int? Row { get; set; }

    public int? Col { get; set; }

    public int? HistoryIndex { get; set; }

    public string? Label { get; set; }

    public string? PlainText { get; set; }

    public byte[]? Rtf { get; set; }

    public byte[]? Html { get; set; }

    public byte[]? PngImage { get; set; }

    public PasteMethod Method { get; set; } = PasteMethod.Direct;

    public string? ColorHex { get; set; }

    public string? Hotkey { get; set; }

    public bool Pinned { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; } = null;
}