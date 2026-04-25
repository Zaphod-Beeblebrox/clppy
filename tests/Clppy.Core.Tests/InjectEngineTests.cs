using System.Collections.Generic;
using System.Linq;
using Clppy.Core.Models;
using Clppy.Core.Paste;
using Xunit;

namespace Clppy.Core.Tests;

public class InjectEngineTests
{
    [Fact]
    public void BuildKeystrokeSequence_String_A_Tab_B_Returns_Unicode_A_VK_TAB_Unicode_B()
    {
        var engine = new InjectPasteEngine();
        var keystrokes = engine.BuildKeystrokeSequence("a\tb");
        
        Assert.Equal(3, keystrokes.Count);
        Assert.Equal('a', (char)keystrokes[0].UnicodeChar);
        Assert.Equal(0x09u, keystrokes[1].VirtualKeyCode); // VK_TAB
        Assert.Equal('b', (char)keystrokes[2].UnicodeChar);
    }

    [Fact]
    public void BuildKeystrokeSequence_String_Hi_Newline_Returns_Hi_VK_RETURN()
    {
        var engine = new InjectPasteEngine();
        var keystrokes = engine.BuildKeystrokeSequence("hi\n");
        
        Assert.Equal(3, keystrokes.Count);
        Assert.Equal('h', (char)keystrokes[0].UnicodeChar);
        Assert.Equal('i', (char)keystrokes[1].UnicodeChar);
        Assert.Equal(0x0Du, keystrokes[2].VirtualKeyCode); // VK_RETURN
    }

    [Fact]
    public void BuildKeystrokeSequence_String_X_CR_LF_Y_Returns_X_VK_RETURN_Y()
    {
        var engine = new InjectPasteEngine();
        var keystrokes = engine.BuildKeystrokeSequence("x\r\ny");

        Assert.Equal(3, keystrokes.Count);
        Assert.Equal('x', (char)keystrokes[0].UnicodeChar);
        Assert.Equal(0x0Du, keystrokes[1].VirtualKeyCode); // VK_RETURN
        Assert.Equal('y', (char)keystrokes[2].UnicodeChar);
    }

    [Fact]
    public void BuildKeystrokeSequence_Empty_String_Returns_Empty_List()
    {
        var engine = new InjectPasteEngine();
        var keystrokes = engine.BuildKeystrokeSequence("");
        
        Assert.Empty(keystrokes);
    }
}
