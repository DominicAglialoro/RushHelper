using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomEntity("rushHelper/rushGoal"), Tracked]
public class RushGoal : Entity {
    public readonly float TimeLimit;

    private readonly string warpTo;
    private readonly Image back;
    private readonly Sprite crystal;
    private readonly Sprite effect;
    private readonly SineWave sine;
    private readonly BloomPoint bloom;
    private readonly TimeDisplay timeDisplay;

    private bool startThisFrame;
    private bool timerStarted;
    private bool failed;
    private bool activated;
    private bool warping;
    private bool demonKilledThisFrame;
    private float timeElapsed;
    private float nextBeepAt;
    private int beepsRemaining;

    public RushGoal(EntityData data, Vector2 offset) : base(data.Position + offset) {
        Collider = new Hitbox(16f, 24f, -8f, -24f);
        Depth = 100;

        warpTo = data.String("warpTo");

        var outline = new Image(GFX.Game["objects/rushHelper/rushGoal/outline"]);

        Add(outline);
        outline.JustifyOrigin(0.5f, 1f);

        Add(back = new Image(GFX.Game["objects/rushHelper/rushGoal/back"]));
        back.Color = (Color.White * 0.25f) with { A = 0 };
        back.JustifyOrigin(0.5f, 1f);

        Add(crystal = new Sprite(GFX.Game, "objects/rushHelper/rushGoal/crystal"));
        crystal.AddLoop("crystal", "", 0.5f);
        crystal.Play("crystal");
        crystal.CenterOrigin();

        Add(effect = new Sprite(GFX.Game, "objects/rushHelper/rushGoal/effect"));
        effect.AddLoop("effect", "", 0.1f);
        effect.Play("effect");
        effect.Color = (Color.White * 0.5f) with { A = 0 };
        effect.JustifyOrigin(0.5f, 1f);

        Add(sine = new SineWave(0.3f));
        sine.Randomize();

        Add(new VertexLight(-12f * Vector2.UnitY, Color.Cyan, 0.5f, 16, 48));
        Add(bloom = new BloomPoint(0.2f, 16f));

        Tag = Tags.FrozenUpdate;
        UpdateCrystalY();

        TimeLimit = data.Float("timeLimit");

        timeDisplay = new TimeDisplay();
    }

    public override void Added(Scene scene) {
        base.Added(scene);

        timeDisplay.Position = Center - 16f * Vector2.UnitY;
        timeDisplay.Visible = false;
        scene.Add(timeDisplay);
    }

    public override void Removed(Scene scene) {
        base.Removed(scene);

        scene.Remove(timeDisplay);
    }

    public override void Update() {
        base.Update();
        UpdateCrystalY();

        if (warping)
            return;

        Scene.OnEndOfFrame += () => {
            if (Scene == null)
                return;

            if (timerStarted)
                timeElapsed += Engine.DeltaTime;
            else if (startThisFrame)
                timerStarted = true;

            bool timedOut = timerStarted && timeElapsed - TimeLimit >= 0.001f;

            if (timedOut)
                Fail();

            if (demonKilledThisFrame) {
                demonKilledThisFrame = false;

                if (!timerStarted && Scene.Tracker.GetEntity<RushStartLine>() != null)
                    Fail();

                if (!failed) {
                    if (Demon.CountLivingDemons(Scene) == 0)
                        SetActivated(true);

                    if (activated)
                        Util.PlaySound("event:/classic/sfx13", 2f);
                    else
                        Util.PlaySound("event:/classic/sfx8", 1.2f);
                }
            }

            if (!failed && beepsRemaining > 0 && timeElapsed >= nextBeepAt) {
                Util.PlaySound("event:/classic/sfx2", 2f);
                beepsRemaining--;
                nextBeepAt += 0.5f;
            }

            var player = CollideFirst<Player>();

            if (player == null)
                return;

            if (!failed && activated) {
                BeginWarp(player);
                Logger.Info("RushHelper", $"Level cleared in {timeElapsed:F}");
            }
            else if (timedOut && !timeDisplay.Visible && Demon.CountLivingDemons(Scene) == 0)
                timeDisplay.Show(timeElapsed - TimeLimit);
        };
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);
        SetActivated(Demon.CountLivingDemons(scene) == 0);
    }

    public void StartTimer() {
        if (failed)
            return;

        timeElapsed = 0f;
        nextBeepAt = TimeLimit - 1.5f;
        beepsRemaining = 3;
        startThisFrame = true;
    }

    public void DemonKilled() => demonKilledThisFrame = true;

    private void UpdateCrystalY() => crystal.Y = bloom.Y = -12f + sine.Value;

    private void SetActivated(bool activated) {
        this.activated = activated;
        back.Visible = activated;
        effect.Visible = activated;
        bloom.Alpha = activated ? 0.5f : 0.2f;
    }

    private void Fail() {
        if (failed)
            return;

        if (!timerStarted)
            Collidable = false;

        failed = true;
        SetActivated(false);
        Scene.Tracker.GetEntity<RushStartLine>()?.Deactivate();
        Util.PlaySound("event:/classic/sfx14", 2f);
        crystal.Color = Color.Red;
    }

    private void BeginWarp(Player player) {
        warping = true;
        Audio.Play(SFX.game_10_glitch_short);
        player.Speed = player.Speed.SafeNormalize() * Math.Min(player.Speed.Length(), 120f);

        var tween = Tween.Create(Tween.TweenMode.Oneshot, null, 0.3f, true);

        tween.UseRawDeltaTime = true;
        tween.OnUpdate = tween => {
            Glitch.Value = 0.5f * tween.Percent;
            Engine.TimeRate = 1f - Ease.ExpoOut(Math.Min(4f * tween.Percent, 1f));
        };
        tween.OnComplete = _ => {
            Glitch.Value = 0.5f;
            Engine.TimeRate = 1f;

            if (!player.Dead)
                WarpToNextLevel(player);
        };

        Add(tween);
    }

    private void WarpToNextLevel(Player player) {
        var level = SceneAs<Level>();

        level.OnEndOfFrame += () => {
            player.CleanUpTriggers();
            level.TeleportTo(player, !string.IsNullOrWhiteSpace(warpTo) ? warpTo : level.GetNextLevel(), Player.IntroTypes.Transition);
            level.Session.FirstLevel = false;
            level.Camera.Position = level.GetFullCameraTargetAt(player, player.Position);

            player.ResetStateValues();
            player.Facing = player.CollideFirst<SpawnFacingTrigger>()?.Facing ?? Facings.Right;

            var tween = Tween.Create(Tween.TweenMode.Oneshot, null, 0.1f, true);

            tween.OnUpdate = tween => Glitch.Value = 0.5f * (1f - tween.Eased);
            player.Add(tween);
        };
    }

    private class TimeDisplay : Entity {
        private float time;

        public TimeDisplay() => Tag = Tags.HUD;

        public override void Render() {
            var goal = Scene.Tracker.GetEntity<RushGoal>();

            if (Scene.Paused || goal == null)
                return;

            var cameraPosition = SceneAs<Level>().Camera.Position;
            var drawPosition = 6f * (Position - cameraPosition);

            ActiveFont.DrawOutline($"+{time:F}", drawPosition, new Vector2(0.5f, 0.5f), 0.75f * Vector2.One, Color.Red, 1f, Color.Black);
        }

        public void Show(float time) {
            this.time = time;
            Visible = true;
        }
    }
}