using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomBackdrop("rushHelper/waterPlane")]
public class WaterPlane : Backdrop {
    private readonly MTexture texture;
    private readonly Wave[] waves;
    private readonly VertexPositionColor[] mesh;
    private readonly int nearY;
    private readonly int farY;
    private readonly float nearScrollY;
    private readonly float farScrollY;

    private float time;

    public WaterPlane(BinaryPacker.Element data) {
        texture = GFX.Game[data.Attr("texture")];
        nearY = data.AttrInt("nearY");
        farY = data.AttrInt("farY");
        nearScrollY = data.AttrFloat("nearScrollY");
        farScrollY = data.AttrFloat("farScrollY");

        float waveNearDensity = data.AttrFloat("waveNearDensity");
        float waveFarDensity = data.AttrFloat("waveFarDensity");
        float waveNearScroll = data.AttrFloat("waveNearScroll");
        float waveFarScroll = data.AttrFloat("waveFarScroll");
        float waveNearSpeed = data.AttrFloat("waveNearSpeed");
        float waveFarSpeed = data.AttrFloat("waveFarSpeed");
        int waveNearWidth = data.AttrInt("waveNearWidth");
        int waveFarWidth = data.AttrInt("waveFarWidth");
        var waveNearColor = Util.HexToColorWithAlpha(data.Attr("waveNearColor"));
        var waveFarColor = Util.HexToColorWithAlpha(data.Attr("waveFarColor"));
        float waveSpeedRandom = data.AttrFloat("waveSpeedRandom");
        float waveWidthRandom = data.AttrFloat("waveWidthRandom");
        float waveAlphaRandom = data.AttrFloat("waveAlphaRandom");

        bool flat = Math.Abs(waveFarDensity - waveNearDensity) < 0.001f;
        float a = (waveFarDensity + waveNearDensity) * (waveFarDensity - waveNearDensity);
        float b = waveNearDensity * waveNearDensity;
        float c = flat ? 1f : 1f / (waveFarDensity - waveNearDensity);

        waves = new Wave[(int) (waveNearDensity + waveFarDensity) / 2];
        mesh = new VertexPositionColor[waves.Length * 6];

        for (int i = 0, quad = 0; i < waves.Length; i++, quad += 6) {
            float depth = (float) i / waves.Length;

            if (!flat)
                depth = MathHelper.Clamp(((float) Math.Sqrt(a * depth + b) - waveNearDensity) * c, 0f, 1f);

            float scroll = MathHelper.Lerp(waveNearScroll, waveFarScroll, depth);
            float speed = MathHelper.Lerp(waveNearSpeed, waveFarSpeed, depth) * Calc.Random.Range(1f - waveSpeedRandom, 1f + waveSpeedRandom);
            int width = (int) Math.Round(MathHelper.Lerp(waveNearWidth, waveFarWidth, depth) * Calc.Random.Range(1f - waveWidthRandom, 1f + waveWidthRandom));
            var color = Color.Lerp(waveNearColor, waveFarColor, depth) * Calc.Random.Range(1f - waveAlphaRandom, 1f);

            for (int j = quad; j < quad + 6; j++)
                mesh[j].Color = color;

            waves[i] = new Wave(depth, Calc.Random.Range(0f, 320f + width), scroll, speed, width);
        }
    }

    public override void Update(Scene scene) {
        base.Update(scene);
        time += Engine.DeltaTime;
    }

    public override void Render(Scene scene) {
        var cameraPosition = ((Level) scene).Camera.Position.Floor();
        float startY = farY - (int) (cameraPosition.Y * farScrollY);
        float endY = nearY - (int) (cameraPosition.Y * nearScrollY);

        Draw.SpriteBatch.Draw(texture.Texture.Texture_Safe, new Vector2(0f, startY), null, Color.White);
        Draw.SpriteBatch.End();

        for (int i = waves.Length - 1, quad = (waves.Length - 1) * 6; i >= 0; i--, quad -= 6) {
            var wave = waves[i];
            float x = wave.XOffset - cameraPosition.X * wave.Scroll + wave.Speed * time;
            float y = MathHelper.Lerp(endY, startY, wave.Depth);
            float width = wave.Width;
            float range = 320f + width;

            x = (x % range + range) % range - width;
            mesh.SetQuad(quad,
                new Vector3(x, y, 0f),
                new Vector3(x, y + 1f, 0f),
                new Vector3(x + width, y, 0f),
                new Vector3(x + width, y + 1f, 0f));
        }

        GFX.DrawVertices(Matrix.Identity, mesh, mesh.Length);
        Draw.SpriteBatch.Begin();
    }

    private struct Wave {
        public readonly float Depth;
        public readonly float XOffset;
        public readonly float Scroll;
        public readonly float Speed;
        public readonly float Width;

        public Wave(float depth, float xOffset, float scroll, float speed, float width) {
            Depth = depth;
            XOffset = xOffset;
            Speed = speed;
            Scroll = scroll;
            Width = width;
        }
    }
}