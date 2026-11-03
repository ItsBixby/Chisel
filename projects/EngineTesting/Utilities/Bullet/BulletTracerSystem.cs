using Engine;
using Engine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EngineTesting.Utilities.Bullet;
public class BulletTracerSystem
{
    private struct Tracer
    {
        public float t;       // Remaining life (counts down from peak)
        public float peak;    // Starting life
        public Vector3 from;
        public Vector3 to;
    }

    private const int PoolSize = 16;
    private readonly Tracer[] tracers = new Tracer[PoolSize];
    private int cursor;

    private static readonly VertexPositionNormalTexture[] Quad = new VertexPositionNormalTexture[6]
    {
            new(new Vector3(-0.5f, -0.5f, 0.1f), Vector3.Forward, new Vector2(1, 0)),
            new(new Vector3(-0.5f,  0.5f, 0.1f), Vector3.Forward, new Vector2(1, 1)),
            new(new Vector3( 0.5f, -0.5f, 0.1f), Vector3.Forward, new Vector2(0, 0)),

            new(new Vector3( 0.5f, -0.5f, 0.1f), Vector3.Forward, new Vector2(0, 0)),
            new(new Vector3(-0.5f,  0.5f, 0.1f), Vector3.Forward, new Vector2(1, 1)),
            new(new Vector3( 0.5f,  0.5f, 0.1f), Vector3.Forward, new Vector2(0, 1)),
    };

    private readonly BasicEffect effect;

    public BulletTracerSystem(GraphicsDevice gd, Texture2D tracerTexture)
    {
        effect = new BasicEffect(gd)
        {
            TextureEnabled = true,
            Texture = tracerTexture,
        };
    }

    public void Spawn(Vector3 from, Vector3 to)
    {
        float dist = Vector3.Distance(from, to);
        tracers[cursor] = new Tracer { t = dist, peak = dist, from = from, to = to };
        cursor = (cursor + 1) % PoolSize;
    }

    public void Draw(GraphicsDevice gd)
    {
        var prevRaster = gd.RasterizerState;
        var prevDepth = gd.DepthStencilState;

        gd.RasterizerState = RasterizerState.CullNone;
        gd.DepthStencilState = DepthStencilState.DepthRead;

        effect.View = RenderEngine.ViewMatrix;
        effect.Projection = RenderEngine.ProjectionMatrix;

        for (int i = 0; i < PoolSize; i++)
        {
            ref var tr = ref tracers[i];
            if (tr.t <= 0f) continue;

            tr.t -= MainEngine.PreviousFrameDelta * 80;

            Vector3 dir = Vector3.Normalize(tr.to - tr.from);
            Vector3 tip = tr.to - dir * tr.t;
            effect.Alpha = 1f;
            effect.World = Matrix.CreateScale(0.3f, 4, 0f)
                           * Matrix.CreateConstrainedBillboard(tip + dir*1.8f, RenderEngine.CameraPosition, dir, RenderEngine.CameraForward, RenderEngine.CameraUp);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleList, Quad, 0, 2);
            }
        }

        gd.RasterizerState = prevRaster;
        gd.DepthStencilState = prevDepth;
    }
}