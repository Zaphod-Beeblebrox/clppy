using System;
using Clppy.Core.Models;
using Clppy.Core.Paste;
using Xunit;

namespace Clppy.Core.Tests;

public class PasteRoutingTests
{
    [Fact]
    public void GetEngine_PasteMethod_Direct_Returns_DirectPasteEngine()
    {
        var router = new PasteRouter();
        var engine = router.GetEngine(PasteMethod.Direct);
        Assert.IsType<DirectPasteEngine>(engine);
    }

    [Fact]
    public void GetEngine_PasteMethod_Inject_Returns_InjectPasteEngine()
    {
        var router = new PasteRouter();
        var engine = router.GetEngine(PasteMethod.Inject);
        Assert.IsType<InjectPasteEngine>(engine);
    }

    [Fact]
    public void GetEngine_Clip_Method_Direct_ForceOpposite_True_Returns_InjectPasteEngine()
    {
        var router = new PasteRouter();
        var clip = new Clip { Method = PasteMethod.Direct };
        var engine = router.GetEngine(clip, true);
        Assert.IsType<InjectPasteEngine>(engine);
    }

    [Fact]
    public void GetEngine_Clip_Method_Inject_ForceOpposite_True_Returns_DirectPasteEngine()
    {
        var router = new PasteRouter();
        var clip = new Clip { Method = PasteMethod.Inject };
        var engine = router.GetEngine(clip, true);
        Assert.IsType<DirectPasteEngine>(engine);
    }

    [Fact]
    public void GetEngine_Clip_Method_Direct_ForceOpposite_False_Returns_DirectPasteEngine()
    {
        var router = new PasteRouter();
        var clip = new Clip { Method = PasteMethod.Direct };
        var engine = router.GetEngine(clip, false);
        Assert.IsType<DirectPasteEngine>(engine);
    }
}
