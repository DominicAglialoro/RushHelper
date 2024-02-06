using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.RushHelper; 

[CustomBackdrop("rushHelper/cloudSea")]
public class CloudSea : Backdrop {
    private const int QUAD_COUNT = 40;
    private const int X_INTERVAL = 320 / QUAD_COUNT;
    
    private Layer[] layers;
    private int nearY;
    private int farY;
    private float nearScrollY;
    private float farScrollY;
    private float layerHeight;
    private bool flip;
    private float time;
    private float[] buffer;
    
    public CloudSea(BinaryPacker.Element data) {
        nearY = data.AttrInt("nearY");
        farY = data.AttrInt("farY");
        nearScrollY = data.AttrFloat("nearScrollY");
        farScrollY = data.AttrFloat("farScrollY");
        
        int layerCount = data.AttrInt("layerCount");
        
        layerHeight = data.AttrFloat("layerHeight");
        
        var layerNearColor = Calc.HexToColor(data.Attr("layerNearColor"));
        var layerFarColor = Calc.HexToColor(data.Attr("layerFarColor"));
        float layerBottomValue = data.AttrFloat("layerBottomValue");
        float waveNearScroll = data.AttrFloat("waveNearScroll");
        float waveFarScroll = data.AttrFloat("waveFarScroll");
        float waveNearScale = data.AttrFloat("waveNearScale");
        float waveFarScale = data.AttrFloat("waveFarScale");
        float waveMinAmplitude = data.AttrFloat("waveMinAmplitude");
        float waveMaxAmplitude = data.AttrFloat("waveMaxAmplitude");
        float waveMinFrequency = data.AttrFloat("waveMinFrequency");
        float waveMaxFrequency = data.AttrFloat("waveMaxFrequency");
        float waveMinSpeed = data.AttrFloat("waveMinSpeed");
        float waveMaxSpeed = data.AttrFloat("waveMaxSpeed");
        int waveCount = data.AttrInt("waveCount");
        
        flip = data.AttrBool("flip");
        layers = new Layer[layerCount];

        for (int i = 0; i < layerCount; i++) {
            var waves = new Wave[waveCount];
            float depth = layerCount > 1 ? (float) i / (layerCount - 1) : 0f;
            float scale = MathHelper.Lerp(waveNearScale, waveFarScale, depth);

            for (int j = 0; j < waveCount; j++) {
                float interp = (float) j / (waveCount + 1);
                
                waves[j] = new Wave(
                    MathHelper.Lerp(waveMaxAmplitude, waveMinAmplitude, interp) * scale,
                    MathHelper.Lerp(waveMinFrequency, waveMaxFrequency, interp) / (320f * scale),
                    Calc.Random.Range(waveMinSpeed, waveMaxSpeed) * (Calc.Random.Next(2) * 2 - 1) * scale,
                    Calc.Random.NextFloat());
            }

            float scroll = MathHelper.Lerp(waveNearScroll, waveFarScroll, depth);
            var mesh = new VertexPositionColor[QUAD_COUNT * 6];
            var topColor = Color.Lerp(layerNearColor, layerFarColor, depth);
            var bottomColor = topColor;

            if (flip)
                topColor = (topColor * layerBottomValue) with { A = 255 };
            else
                bottomColor = (bottomColor * layerBottomValue) with { A = 255 };

            for (int quad = 0; quad < mesh.Length; quad += 6) {
                mesh[quad].Color = topColor;
                mesh[quad + 1].Color = bottomColor;
                mesh[quad + 2].Color = topColor;
                mesh[quad + 3].Color = bottomColor;
                mesh[quad + 4].Color = topColor;
                mesh[quad + 5].Color = bottomColor;
            }

            layers[i] = new Layer(depth, scroll, waves, mesh);
        }

        buffer = new float[QUAD_COUNT + 1];
    }
    
    public override void Update(Scene scene) {
        base.Update(scene);
        time += Engine.DeltaTime;
    }

    public override void Render(Scene scene) {
        Draw.SpriteBatch.End();

        var cameraPosition = ((Level) scene).Camera.Position.Floor();
        float startY = farY - (int) (cameraPosition.Y * farScrollY);
        float endY = nearY - (int) (cameraPosition.Y * nearScrollY);

        for (int i = layers.Length - 1; i >= 0; i--) {
            var layer = layers[i];
            var waves = layer.Waves;
            float offset = -cameraPosition.X * layer.Scroll;
            
            for (int j = 0; j < buffer.Length; j++) {
                float x = 320f * j / (buffer.Length - 1) - offset;
                float sum = 0f;
            
                foreach (var wave in waves)
                    sum += wave.Amplitude * (float) Math.Sin(MathHelper.TwoPi * (wave.Frequency * (x - time * wave.Speed) + wave.Phase));
            
                buffer[j] = sum;
            }

            var mesh = layer.Mesh;
            float y = MathHelper.Lerp(endY, startY, layer.Depth);

            if (flip) {
                for (int quad = 0, x = 0, k = 0; quad < mesh.Length; quad += 6, x += X_INTERVAL, k++) {
                    mesh.SetQuad(quad,
                        new Vector3(x, y, 0f),
                        new Vector3(x, y + layerHeight + buffer[k], 0f),
                        new Vector3(x + X_INTERVAL, y, 0f),
                        new Vector3(x + X_INTERVAL, y + layerHeight + buffer[k + 1], 0f));
                }
            }
            else {
                for (int quad = 0, x = 0, k = 0; quad < mesh.Length; quad += 6, x += X_INTERVAL, k++) {
                    mesh.SetQuad(quad,
                        new Vector3(x, y + buffer[k], 0f),
                        new Vector3(x, y + layerHeight, 0f),
                        new Vector3(x + X_INTERVAL, y + buffer[k + 1], 0f),
                        new Vector3(x + X_INTERVAL, y + layerHeight, 0f));
                }
            }

            GFX.DrawVertices(Matrix.Identity, mesh, mesh.Length);
        }
        
        Draw.SpriteBatch.Begin();
    }

    private struct Layer {
        public readonly float Depth;
        public readonly float Scroll;
        public readonly Wave[] Waves;
        public readonly VertexPositionColor[] Mesh;

        public Layer(float depth, float scroll, Wave[] waves, VertexPositionColor[] mesh) {
            Depth = depth;
            Scroll = scroll;
            Waves = waves;
            Mesh = mesh;
        }
    }

    private struct Wave {
        public readonly float Amplitude;
        public readonly float Frequency;
        public readonly float Speed;
        public readonly float Phase;

        public Wave(float amplitude, float frequency, float speed, float phase) {
            Amplitude = amplitude;
            Frequency = frequency;
            Speed = speed;
            Phase = phase;
        }
    }
}