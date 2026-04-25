using System;
using System.Collections.Generic;
using System.Linq;
using Clppy.Core.Models;

namespace Clppy.Core.Clipboard;

public class HistoryBuffer
{
    private readonly int _capacity;
    private readonly List<Clip> _items;

    public HistoryBuffer(int capacity = 20)
    {
        _capacity = capacity;
        _items = new List<Clip>();
    }

    public void Add(Clip clip)
    {
        if (clip == null) return;

        _items.Insert(0, clip);

        if (_items.Count > _capacity)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    public IEnumerable<Clip> Items => _items.AsReadOnly();

    public int Count => _items.Count;

    public int Capacity => _capacity;
}