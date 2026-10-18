using System;
using System.Reflection;
using UnityEngine;
using Verse;
using Xenomorphtype;

// Factory-path regression for reinstall previews, without requiring textures or a renderer.
public static class AnimatedGraphicChecks
{
    private static int checks;
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
        checks++;
    }
    public static void Main()
    {
        typeof(UnityData).GetField("mainThreadId", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(null, Environment.CurrentManagedThreadId);
        Type adapter = typeof(XMTAnimatedBuilding.AnimatedGraphic);
        MethodInfo factory = typeof(GraphicDatabase).GetMethod("GetInner", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Check("vanilla graphic factory accepts animation adapter", factory.MakeGenericMethod(adapter) != null);
        var originalData = new GraphicData { graphicClass = typeof(FixtureGraphic), drawSize = new Vector2(2f, 3f), shadowData = new ShadowData() };
        var source = new FixtureGraphic();
        source.Init(new GraphicRequest { graphicClass = typeof(FixtureGraphic), path = "AnimatedGraphicFixture", graphicData = originalData,
            color = Color.white, colorTwo = Color.white, drawSize = originalData.drawSize });
        var animated = new XMTAnimatedBuilding.AnimatedGraphic(source) { Scale = new Vector2(1.5f, 2f) };
        Check("adapter copies shared metadata", animated.data != originalData && originalData.graphicClass == typeof(FixtureGraphic));
        var ghostData = new GraphicData();
        ghostData.CopyFrom(animated.data);
        ghostData.shadowData = null;
        var ghost = (XMTAnimatedBuilding.AnimatedGraphic)GraphicDatabase.Get(adapter, animated.path, null, animated.drawSize,
            Color.red, Color.white, ghostData, null, null);
        Check("factory rebuilds underlying graphic type", ghost.Source is FixtureGraphic);
        Check("ghost tint reaches underlying source", ghost.Source.Color == Color.red);
        Check("ghost size survives reconstruction", ghost.drawSize == animated.drawSize);
        Check("ghost suppresses shadows without modifying original", ghost.Source.data.shadowData == null && originalData.shadowData != null);
        var recolored = (XMTAnimatedBuilding.AnimatedGraphic)animated.GetColoredVersion(null, Color.blue, Color.white);
        Check("recoloring delegates to source", recolored.Source.Color == Color.blue);
        Check("recoloring retains per-instance animation scale", recolored.Scale == animated.Scale);
        Check("original source remains unchanged", source.Color == Color.white);
        Console.WriteLine("ALL " + checks + " GRAPHICS CHECKS PASSED");
    }
}
public sealed class FixtureGraphic : Graphic
{
    public override void Init(GraphicRequest req)
    {
        data = req.graphicData; path = req.path; drawSize = req.drawSize; color = req.color; colorTwo = req.colorTwo;
    }
    public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
    {
        var graphic = new FixtureGraphic();
        graphic.Init(new GraphicRequest { graphicClass = typeof(FixtureGraphic), graphicData = data, path = path,
            drawSize = drawSize, shader = shader, color = color, colorTwo = colorTwo });
        return graphic;
    }
}