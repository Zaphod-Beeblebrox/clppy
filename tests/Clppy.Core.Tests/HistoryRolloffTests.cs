using System;
using System.Linq;
using Clppy.Core.Models;
using Clppy.Core.Clipboard;
using Xunit;

namespace Clppy.Core.Tests;

public class HistoryRolloffTests
{
    [Fact]
    public void Add_21_Items_With_Capacity_20_Holds_Exactly_20()
    {
        var buffer = new HistoryBuffer(20);
        
        // Add 21 items
        for (int i = 0; i < 21; i++)
        {
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Label = $"Clip {i}"
            };
            buffer.Add(clip);
        }
        
        // Should only hold 20 items
        Assert.Equal(20, buffer.Count);
        Assert.Equal(20, buffer.Capacity);
    }

    [Fact]
    public void Add_To_Empty_Buffer_Added_Item_At_Index_0()
    {
        var buffer = new HistoryBuffer(20);
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Label = "First Clip"
        };
        
        buffer.Add(clip);
        
        Assert.Equal(1, buffer.Count);
        Assert.Equal(clip, buffer.Items.First());
    }

    [Fact]
    public void Items_Returned_In_Insertion_Recency_Order()
    {
        var buffer = new HistoryBuffer(5);
        
        var clip1 = new Clip { Id = Guid.NewGuid(), Label = "Clip 1" };
        var clip2 = new Clip { Id = Guid.NewGuid(), Label = "Clip 2" };
        var clip3 = new Clip { Id = Guid.NewGuid(), Label = "Clip 3" };
        
        buffer.Add(clip1);
        buffer.Add(clip2);
        buffer.Add(clip3);
        
        var items = buffer.Items.ToList();
        
        // Newest first
        Assert.Equal(clip3, items[0]); // newest
        Assert.Equal(clip2, items[1]);
        Assert.Equal(clip1, items[2]); // oldest
    }
}