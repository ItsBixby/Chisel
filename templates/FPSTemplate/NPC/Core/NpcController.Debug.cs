using Chisel.Utils;
using Engine;
using Engine.Rendering;
using Engine.Utils;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System;
using System.Collections.Generic;

namespace FPSTemplate.NPC.Core;

public partial class NpcController
{
    private static readonly List<NpcController> allNpcs = new();
    public static IReadOnlyList<NpcController> AllNpcs => allNpcs;

    private static BasicEffect debugEffect;

    private static BasicEffect GetDebugEffect() =>
        debugEffect ??= new BasicEffect(MainEngine.Instance.GraphicsDevice)
        {
            TextureEnabled = false,
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

    private void DrawDebug()
    {
        if (!ShouldDebugDraw()) return;

        var verts = new List<VertexPositionColor>();

        AppendStateIndicator(verts);
        AppendSightCone(verts);
        AppendHearRadius(verts);
        AppendThreatLines(verts);
        AppendLastKnownPosition(verts);

        base.DebugRender();

        if (verts.Count < 2) return;

        var fx = GetDebugEffect();
        fx.World = RenderEngine.WorldMatrix;
        fx.View = RenderEngine.ViewMatrix;
        fx.Projection = RenderEngine.ProjectionMatrix;

        foreach (var pass in fx.CurrentTechnique.Passes)
        {
            pass.Apply();
            MainEngine.Instance.GraphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList, verts.ToArray(), 0, verts.Count / 2);
        }
    }

    public void DrawDebugCaption(SpriteBatch spriteBatch)
    {
        if (!ShouldDebugDraw()) return;

        var headWorld = entity.Position + Vector3.Up * (Definition.Bounds.Max.Y + 0.1f);
        var viewport = MainEngine.Instance.GraphicsDevice.Viewport;
        var screen = viewport.Project(headWorld,
            RenderEngine.ProjectionMatrix, RenderEngine.ViewMatrix, RenderEngine.WorldMatrix);

        if (screen.Z < 0f || screen.Z > 1f) return;

        var font = RenderEngine.FontSystem?.GetFont(18f);
        if (font == null) return;

        string debugStr = $"Cur state: {currentStateName}";

        var size = font.MeasureString(debugStr);
        var pos = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y - 4f);

        spriteBatch.DrawString(font, debugStr, pos + Vector2.UnitY * -32 + Vector2.One * 1.5f, Color.Black);
        spriteBatch.DrawString(font, debugStr, pos + Vector2.UnitY * -32 - Vector2.One * 1.5f, Color.Black);
        spriteBatch.DrawString(font, debugStr, pos + Vector2.UnitY * -32, Color.White);
    }

    private bool ShouldDebugDraw()
    {
        if (IsDead) return false;
        if (!GameEngine.AIDebug && !GameEngine.AIDebugNearest) return false;
        if (!GameEngine.AIDebugNearest) return true;

        float myDist = Vector3.Distance(entity.Position, MainEngine.Instance.CameraPosition);
        foreach (var other in allNpcs)
        {
            if (other == this || other.IsDead) continue;
            if (Vector3.Distance(other.entity.Position, MainEngine.Instance.CameraPosition) < myDist)
                return false;
        }
        return true;
    }

    private void AppendStateIndicator(List<VertexPositionColor> v)
    {
        string label = currentStateName ?? "none";
        float top = entity.GetRealBounds().Max.Y;
        Vector3 p = entity.Position + Vector3.Up * (top + 0.35f);
        float sz = 0.18f;

        int hash = label.GetHashCode();
        var color = new Color((hash >> 16) & 0xFF, (hash >> 8) & 0xFF, hash & 0xFF);

        Line(v, p - Vector3.Right * sz,   p + Vector3.Right * sz,   color);
        Line(v, p - Vector3.Up * sz,      p + Vector3.Up * sz,      color);
        Line(v, p - Vector3.Forward * sz, p + Vector3.Forward * sz, color);
        Line(v, entity.Position, entity.Position + Vector3.Up * top, color * 0.3f);

        if (Squad != null)
        {
            foreach (var m in Squad.Members)
            {
                if (m == this || m.entity == null || m.IsDead) continue;
                Line(v, entity.Position, m.entity.Position, new Color(80, 200, 80));
            }
        }
    }

    private void AppendSightCone(List<VertexPositionColor> v)
    {
        Color col = Color.Cyan * 0.5f;
        Vector3 eye = entity.Position;
        Vector3 forward = -Matrix.CreateRotationY(aiLookAngle * CMath.Deg2Rad).Forward;
        float halfRad = MathHelper.ToRadians(Definition.SightFOVDegrees * 0.5f);
        float range = Definition.SightRange;

        Vector3 prev = Vector3.Zero;
        for (int i = 0; i <= 16; i++)
        {
            float t = (float)i / 16;
            float ang = MathHelper.Lerp(-halfRad, halfRad, t);
            Vector3 pt = eye + Vector3.Transform(forward, Matrix.CreateRotationY(ang)) * range;

            if (i > 0) Line(v, prev, pt, col);
            if (i == 0 || i == 8 || i == 16) Line(v, eye, pt, col);
            prev = pt;
        }
    }

    private void AppendHearRadius(List<VertexPositionColor> v) =>
        AppendCircleXZ(v, entity.Position + Vector3.Up * 0.05f,
                       Definition.HearRange, 24, Color.Yellow * 0.35f);

    private void AppendThreatLines(List<VertexPositionColor> v)
    {
        Vector3 myEye = entity.Position + Vector3.Up * -0.1f;
        foreach (var t in Threats)
        {
            Color c = t.IsCurrentlyVisible ? Color.Lime
                    : t.IsRecent          ? Color.Yellow
                    :                       Color.Gray * 0.6f;

            Line(v, myEye, t.Entity.Position + Vector3.Up * -0.1f, c * 0.8f);
            AppendDiamond(v, t.LastKnownPosition, 0.15f, c * 0.5f);
        }
    }

    private void AppendLastKnownPosition(List<VertexPositionColor> v)
    {
        if (!IsInCombat && currentStateName != "search") return;

        Vector3 p = LastKnownTargetPosition;
        AppendDiamond(v, p, 0.28f, Color.Magenta * 0.75f);
        Line(v, p, p + Vector3.Up * 2.2f, Color.Magenta * 0.4f);
        AppendCircleXZ(v, p, 0.4f, 12, Color.Magenta * 0.3f);
    }

    private static void Line(List<VertexPositionColor> v, Vector3 a, Vector3 b, Color c)
    {
        v.Add(new VertexPositionColor(a, c));
        v.Add(new VertexPositionColor(b, c));
    }

    private static void AppendCircleXZ(List<VertexPositionColor> v, Vector3 center, float radius, int segs, Color c)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segs; i++)
        {
            float a = MathF.PI * 2f * i / segs;
            Vector3 pt = center + new Vector3(MathF.Cos(a) * radius, 0f, MathF.Sin(a) * radius);
            Line(v, prev, pt, c);
            prev = pt;
        }
    }

    private static void AppendDiamond(List<VertexPositionColor> v, Vector3 center, float size, Color c)
    {
        Line(v, center - Vector3.Up * size,      center + Vector3.Up * size,      c);
        Line(v, center - Vector3.Right * size,   center + Vector3.Right * size,   c);
        Line(v, center - Vector3.Forward * size, center + Vector3.Forward * size, c);
    }
}
