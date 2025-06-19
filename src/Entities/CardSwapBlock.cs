using System;
using Celeste.Mod.Entities;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomEntity("rushHelper/cardSwapBlock"), Tracked]
public class CardSwapBlock : Solid {
    private static void DrawBlockStyle(Vector2 pos, float width, float height, MTexture[,] ninSlice, Sprite middle, Color color) {
        int num = (int) (width / 8f);
        int num2 = (int) (height / 8f);

        ninSlice[0, 0].Draw(pos + new Vector2(0f, 0f), Vector2.Zero, color);
        ninSlice[2, 0].Draw(pos + new Vector2(width - 8f, 0f), Vector2.Zero, color);
        ninSlice[0, 2].Draw(pos + new Vector2(0f, height - 8f), Vector2.Zero, color);
        ninSlice[2, 2].Draw(pos + new Vector2(width - 8f, height - 8f), Vector2.Zero, color);

        for (int i = 1; i < num - 1; i++) {
            ninSlice[1, 0].Draw(pos + new Vector2(i * 8, 0f), Vector2.Zero, color);
            ninSlice[1, 2].Draw(pos + new Vector2(i * 8, height - 8f), Vector2.Zero, color);
        }

        for (int j = 1; j < num2 - 1; j++) {
            ninSlice[0, 1].Draw(pos + new Vector2(0f, j * 8), Vector2.Zero, color);
            ninSlice[2, 1].Draw(pos + new Vector2(width - 8f, j * 8), Vector2.Zero, color);
        }

        for (int k = 1; k < num - 1; k++) {
            for (int l = 1; l < num2 - 1; l++)
                ninSlice[1, 1].Draw(pos + new Vector2(k, l) * 8f, Vector2.Zero, color);
        }

        if (middle == null)
            return;

        middle.Color = color;
        middle.RenderPosition = pos + new Vector2(width / 2f, height / 2f);
        middle.Render();
    }

    public Vector2 Direction;
    public bool Swapping;

    private readonly Vector2 start;
    private readonly Vector2 end;
    private readonly float speed;
    private readonly string directory;
    private readonly MTexture[,] nineSliceGreen;
    private readonly MTexture[,] nineSliceRed;
    private readonly MTexture[,] nineSliceTarget;
    private readonly Sprite middleGreen;
    private readonly Sprite middleRed;
    private readonly bool renderBg;
    private readonly ParticleType particleType;
    private readonly bool emitParticles;
    private readonly string moveSFX;
    private readonly string moveEndSFX;

    private float lerp;
    private int target;
    private Rectangle moveRect;
    private float redAlpha;
    private EventInstance moveSfx;
    private DisplacementRenderer.Burst burst;
    private float particlesRemainder;
    private PathRenderer path;

    public CardSwapBlock(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, data.Height, false) {
        var node = data.Nodes[0] + offset;

        start = Position;
        end = node;

        if (start == end)
            speed = 0f;
        else
            speed = data.Float("speed", 360f) / Vector2.Distance(start, end);

        Direction.X = Math.Sign(end.X - start.X);
        Direction.Y = Math.Sign(end.Y - start.Y);

        if (data.Bool("dashTrigger"))
            Add(new DashListener(OnDash));

        Add(new UseCardListener(_ => OnDash(Vector2.Zero)));

        redAlpha = 1f;

        int left = (int) MathHelper.Min(X, node.X);
        int top = (int) MathHelper.Min(Y, node.Y);
        int right = (int) MathHelper.Max(X + Width, node.X + Width);
        int bottom = (int) MathHelper.Max(Y + Height, node.Y + Height);

        moveRect = new Rectangle(left, top, right - left, bottom - top);

        directory = data.Attr("directory", "objects/swapBlock");

        var blockTexture = GFX.Game[directory + "/block"];
        var blockRedTexture = GFX.Game[directory + "/blockRed"];
        var targetTexture = GFX.Game[directory + "/target"];

        nineSliceGreen = new MTexture[3, 3];
        nineSliceRed = new MTexture[3, 3];
        nineSliceTarget = new MTexture[3, 3];

        for (int i = 0; i < 3; i++) {
            for (int j = 0; j < 3; j++) {
                nineSliceGreen[i, j] = blockTexture.GetSubtexture(new Rectangle(i * 8, j * 8, 8, 8));
                nineSliceRed[i, j] = blockRedTexture.GetSubtexture(new Rectangle(i * 8, j * 8, 8, 8));
                nineSliceTarget[i, j] = targetTexture.GetSubtexture(new Rectangle(i * 8, j * 8, 8, 8));
            }
        }

        middleGreen = new Sprite(GFX.Game, directory + "/midBlock");
        middleGreen.AddLoop("idle", "", 0.08f);
        middleGreen.Justify = new Vector2(0.5f, 0.5f);
        middleGreen.Play("idle");
        Add(middleGreen);
        middleRed = new Sprite(GFX.Game, directory + "/midBlockRed");
        middleRed.AddLoop("idle", "", 0.08f);
        middleRed.Justify = new Vector2(0.5f, 0.5f);
        middleRed.Play("idle");

        Add(middleRed);
        Add(new LightOcclude(0.2f));
        Depth = -9999;

        renderBg = data.Bool("renderBG");

        particleType = new ParticleType(SwapBlock.P_Move) {
            Color = Calc.HexToColor(data.Attr("particleColor1", "fbf236")),
            Color2 = Calc.HexToColor(data.Attr("particleColor2", "6abe30"))
        };

        emitParticles = data.Bool("emitParticles", true);
        moveSFX = data.Attr("moveSFX", "event:/game/05_mirror_temple/swapblock_move");
        moveEndSFX = data.Attr("moveEndSFX", "event:/game/05_mirror_temple/swapblock_move_end");
    }

    public override void Added(Scene scene) {
        base.Added(scene);

        scene.Add(path = new PathRenderer(this));
    }

    public override void Removed(Scene scene) {
        base.Removed(scene);

        Audio.Stop(moveSfx);
        scene.Remove(path);
        path = null;
    }

    public override void SceneEnd(Scene scene) {
        base.SceneEnd(scene);

        Audio.Stop(moveSfx);
    }

    private void OnDash(Vector2 direction) {
        Swapping = lerp < 1f;
        target = 1 - target;
        burst = SceneAs<Level>().Displacement.AddBurst(Center, 0.2f, 0f, 16f);

        Audio.Stop(moveSfx);
        moveSfx = Audio.Play(moveSFX, Center);

        if (!Swapping)
            Audio.Play(moveEndSFX, Center);
    }

    public override void Update() {
        base.Update();

        if (burst != null)
            burst.Position = Center;

        redAlpha = Calc.Approach(redAlpha, 1 - target, Engine.DeltaTime * 32f);

        if (target == 0 && lerp == 0f) {
            middleRed.SetAnimationFrame(0);
            middleGreen.SetAnimationFrame(0);
        }

        float previousLerp = lerp;

        if (start == end)
            lerp = target;
        else
            lerp = Calc.Approach(lerp, target, speed * Engine.DeltaTime);

        if (lerp != previousLerp) {
            var velocity = (end - start) * speed * (2 * target - 1);
            var previousPosition = Position;

            if (Scene.OnInterval(0.02f)) {
                MoveParticles(target switch {
                    1 => end - start,
                    _ => start - end,
                });
            }

            if (start != end)
                MoveTo(Vector2.Lerp(start, end, lerp), velocity);

            if (previousPosition != Position) {
                Audio.Position(moveSfx, Center);

                if (Position == start && target == 0)
                    Audio.Play(moveEndSFX, Center);
                else if (Position == end && target == 1) {
                    Audio.SetParameter(moveSfx, "end", 1f);
                    Audio.Play(moveEndSFX, Center);
                }
            }
        }

        bool atEnd = target == 1 ? lerp >= 1f : lerp <= 0f;

        if (Swapping && atEnd)
            Swapping = false;

        StopPlayerRunIntoAnimation = atEnd;
    }

    private void MoveParticles(Vector2 normal) {
        if (!emitParticles)
            return;

        Vector2 position;
        Vector2 positionRange;
        float direction;
        float newParticles;

        if (normal.X > 0f) {
            position = CenterLeft;
            positionRange = Vector2.UnitY * (Height - 6f);
            direction = 3.14159274f;
            newParticles = Math.Max(2f, Height / 14f);
        } else if (normal.X < 0f) {
            position = CenterRight;
            positionRange = Vector2.UnitY * (Height - 6f);
            direction = 0f;
            newParticles = Math.Max(2f, Height / 14f);
        } else if (normal.Y > 0f) {
            position = TopCenter;
            positionRange = Vector2.UnitX * (Width - 6f);
            direction = -1.57079637f;
            newParticles = Math.Max(2f, Width / 14f);
        } else {
            position = BottomCenter;
            positionRange = Vector2.UnitX * (Width - 6f);
            direction = 1.57079637f;
            newParticles = Math.Max(2f, Width / 14f);
        }

        particlesRemainder += newParticles;

        int particleAmt = (int) particlesRemainder;

        particlesRemainder -= particleAmt;
        positionRange *= 0.5f;

        SceneAs<Level>().Particles.Emit(particleType, particleAmt, position, positionRange, direction);
    }

    public override void Render() {
        var shakePosition = Position + Shake;

        if (lerp != target && speed > 0f) {
            var direction = (end - start).SafeNormalize();

            if (target == 1)
                direction *= -1f;

            int num3 = 2;

            while (num3 < 16f) {
                DrawBlockStyle(shakePosition + direction * num3, Width, Height, nineSliceGreen, middleGreen, Color.White * (1f - num3 / 16f));
                num3 += 2;
            }
        }

        if (redAlpha < 1f)
            DrawBlockStyle(shakePosition, Width, Height, nineSliceGreen, middleGreen, Color.White);

        if (redAlpha > 0f)
            DrawBlockStyle(shakePosition, Width, Height, nineSliceRed, middleRed, Color.White * redAlpha);
    }

    private class PathRenderer : Entity {
        private readonly CardSwapBlock block;
        private readonly MTexture pathTexture;
        private readonly MTexture clipTexture;

        private float timer;

        public PathRenderer(CardSwapBlock block) {
            clipTexture = new MTexture();
            this.block = block;
            Depth = 8999;
            pathTexture = GFX.Game[block.directory + "/path" + (block.start.X == block.end.X ? "V" : "H")];
            timer = Calc.Random.NextFloat();
        }

        public override void Update() {
            base.Update();

            timer += Engine.DeltaTime * 4f;
        }

        public override void Render() {
            for (int i = block.moveRect.Left; i < block.moveRect.Right; i += pathTexture.Width) {
                for (int j = block.moveRect.Top; j < block.moveRect.Bottom; j += pathTexture.Height) {
                    pathTexture.GetSubtexture(0, 0, Math.Min(pathTexture.Width, block.moveRect.Right - i), Math.Min(pathTexture.Height, block.moveRect.Bottom - j), clipTexture);

                    if (block.renderBg)
                        clipTexture.DrawCentered(new Vector2(i + clipTexture.Width / 2, j + clipTexture.Height / 2), Color.White);
                }
            }

            float scale = 0.5f * (0.5f + ((float) Math.Sin(timer) + 1f) * 0.25f);

            DrawBlockStyle(new Vector2(block.moveRect.X, block.moveRect.Y), block.moveRect.Width, block.moveRect.Height, block.nineSliceTarget, null, Color.White * scale);
        }
    }
}