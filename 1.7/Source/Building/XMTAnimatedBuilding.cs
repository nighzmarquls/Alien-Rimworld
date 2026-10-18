using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompProperties_Breathing : CompProperties
    {
        public float amplitude = 0.01f;
        public int periodTicks = 240;
        public Vector2 axes = Vector2.one;
        public CompProperties_Breathing() { compClass = typeof(CompBreathing); }
    }
    public class CompBreathing : ThingComp
    {
        public Vector2 Scale
        {
            get
            {
                CompProperties_Breathing settings = (CompProperties_Breathing)props;
                float phase = (Find.TickManager.TicksGame + parent.thingIDNumber % 240)
                    * Mathf.PI * 2f / Mathf.Max(1, settings.periodTicks);
                return Vector2.one + settings.axes * (Mathf.Sin(phase) * settings.amplitude);
            }
        }
    }

    public abstract class XMTAnimatedBuilding : XMTBase_Building
    {
        private AnimatedGraphic animated;
        protected virtual Color AnimationColor => base.Graphic.Color;
        public override Graphic Graphic
        {
            get
            {
                Graphic source = base.Graphic;
                Color tint = AnimationColor;
                if (tint != source.Color) source = source.GetColoredVersion(source.Shader, tint, source.ColorTwo);
                if (animated == null || animated.Source != source) animated = new AnimatedGraphic(source);
                float growth = GetComp<CompOrganicGrowth>()?.VisualScale ?? 1f;
                animated.Scale = (GetComp<CompBreathing>()?.Scale ?? Vector2.one) * growth;
                return animated;
            }
        }

        // A per-instance adapter keeps shared graphics/materials and fixed meshes immutable.
        // Vanilla drawing still supplies offsets, rotation, shadows, silhouettes and comp hooks.
        public sealed class AnimatedGraphic : Graphic
        {
            public Graphic Source { get; private set; }
            public Vector2 Scale = Vector2.one;
            // GhostUtility recreates the runtime graphic type through GraphicDatabase's new() factory.
            public AnimatedGraphic() { }
            public AnimatedGraphic(Graphic source) => SetSource(source);
            private void SetSource(Graphic source)
            {
                Source = source;
                // Carry the underlying type through vanilla's copied ghost metadata, without
                // changing the shared source's GraphicData or trying to cache this instance.
                data = new GraphicData();
                if (source.data != null) data.CopyFrom(source.data);
                data.graphicClass = source.GetType();
                path = source.path;
                drawSize = source.drawSize;
                color = source.Color;
                colorTwo = source.ColorTwo;
                ShadowGraphic = source.ShadowGraphic;
            }
            public override void Init(GraphicRequest req)
            {
                System.Type sourceClass = req.graphicData?.graphicClass;
                if (sourceClass == null || sourceClass == typeof(AnimatedGraphic))
                    throw new System.ArgumentException("Animated graphics require underlying graphic type metadata.");
                SetSource(GraphicDatabase.Get(sourceClass, req.path, req.shader, req.drawSize,
                    req.color, req.colorTwo, req.graphicData, req.shaderParameters, req.maskPath));
            }
            public override Graphic GetColoredVersion(Shader shader, Color color, Color colorTwo)
                => new AnimatedGraphic(Source.GetColoredVersion(shader, color, colorTwo)) { Scale = Scale };
            public override bool ShouldDrawRotated => Source.ShouldDrawRotated;
            public override float DrawRotatedExtraAngleOffset => Source.DrawRotatedExtraAngleOffset;
            public override bool WestFlipped => Source.WestFlipped;
            public override bool EastFlipped => Source.EastFlipped;
            public override bool UseSameGraphicForGhost => Source.UseSameGraphicForGhost;
            public override Material MatSingle => Source.MatSingle;
            public override Material MatNorth => Source.MatNorth;
            public override Material MatEast => Source.MatEast;
            public override Material MatSouth => Source.MatSouth;
            public override Material MatWest => Source.MatWest;
            public override Mesh MeshAt(Rot4 rot) => Source.MeshAt(rot);
            public override Material MatAt(Rot4 rot, Thing thing = null) => Source.MatAt(rot, thing);
            protected override void DrawMeshInt(Mesh mesh, Vector3 loc, Quaternion quat, Material mat)
            {
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(loc, quat, new Vector3(Scale.x, 1f, Scale.y)), mat, 0);
            }
        }
    }
}
