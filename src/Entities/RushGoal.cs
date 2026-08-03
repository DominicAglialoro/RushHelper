using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomEntity("rushHelper/rushGoal"), Tracked]
public class RushGoal : Entity {
    private enum State {
        Inactive,
        Active,
        Failed,
        Warping
    }

    public readonly int TimeLimitMillis;

    private readonly string warpTo;
    private readonly string warpToWithGolden;
    private readonly Image back;
    private readonly Sprite crystal;
    private readonly Sprite effect;
    private readonly SineWave sine;
    private readonly BloomPoint bloom;
    private readonly TimeDisplay timeDisplay;
    private readonly TimeRateModifier timeRateModifier;

    private bool startThisFrame;
    private bool timerStarted;
    private State state = State.Inactive;
    private bool demonKilledThisFrame;
    private double timeElapsed;
    private int nextBeepAt;

    public RushGoal(EntityData data, Vector2 offset) : base(data.Position + offset) {
        Collider = new Hitbox(16f, 24f, -8f, -24f);
        Depth = 100;

        warpTo = data.String("warpTo");
        warpToWithGolden = data.String("warpToWithGolden");

        var outline = new Image(GFX.Game["objects/rushHelper/rushGoal/outline"]);

        Add(outline);
        outline.JustifyOrigin(0.5f, 1f);

        Add(back = new Image(GFX.Game["objects/rushHelper/rushGoal/back"]));
        back.Color = (Color.White * 0.25f) with { A = 0 };
        back.JustifyOrigin(0.5f, 1f);
        back.Visible = false;

        Add(crystal = new Sprite(GFX.Game, "objects/rushHelper/rushGoal/crystal"));
        crystal.AddLoop("crystal", "", 0.5f);
        crystal.Play("crystal");
        crystal.CenterOrigin();

        Add(effect = new Sprite(GFX.Game, "objects/rushHelper/rushGoal/effect"));
        effect.AddLoop("effect", "", 0.1f);
        effect.Play("effect");
        effect.Color = (Color.White * 0.5f) with { A = 0 };
        effect.JustifyOrigin(0.5f, 1f);
        effect.Visible = false;

        Add(sine = new SineWave(0.3f));
        sine.Randomize();

        Add(new VertexLight(-12f * Vector2.UnitY, Color.Cyan, 0.5f, 16, 48));
        Add(bloom = new BloomPoint(0.2f, 16f));
        bloom.Alpha = 0.2f;

        Tag = Tags.FrozenUpdate;
        UpdateCrystalY();

        TimeLimitMillis = (int) Math.Round(1000d * data.Float("timeLimit"));

        timeDisplay = new TimeDisplay(Color.Red);
        Add(timeRateModifier = new TimeRateModifier(0.1f, false));
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
        Scene.OnEndOfFrame += LateUpdate;
    }

    private void LateUpdate() {
        if (Scene == null || state == State.Warping)
            return;

        if (timerStarted)
            timeElapsed += Engine.DeltaTime;
        else if (startThisFrame)
            timerStarted = true;

        int timeElapsedMillis = (int) Math.Round(1000d * timeElapsed);
        bool timedOut = timerStarted && timeElapsedMillis > TimeLimitMillis;

        if (state != State.Failed && timedOut)
            Fail();

        if (state != State.Failed && demonKilledThisFrame) {
            demonKilledThisFrame = false;

            if (timerStarted || Scene.Tracker.GetEntity<RushStartLine>() == null) {
                if (Demon.CountLivingDemons(Scene) == 0) {
                    Activate();
                    Util.PlaySound("event:/classic/sfx13", 2f);
                }
                else
                    Util.PlaySound("event:/classic/sfx8", 1.2f);
            }
            else
                Fail();
        }

        if (state != State.Failed && timerStarted && nextBeepAt < TimeLimitMillis && timeElapsedMillis >= nextBeepAt) {
            Util.PlaySound("event:/classic/sfx2", 2f);
            nextBeepAt += 500;
        }

        var player = CollideFirst<Player>();

        if (player == null)
            return;

        if (state == State.Active) {
            WarpPlayer(player);

            if (!timerStarted)
                return;

            Logger.Info("RushHelper", $"Level cleared in {Util.HundredthsToString(timeElapsedMillis / 10)}");

            if (RushHelperModule.Settings.ShowTimeRemainingOnClear)
                Scene.Add(new LevelClearedTimeRemainingDisplay($"-{Util.HundredthsToString((TimeLimitMillis - timeElapsedMillis) / 10)}"));
        }
        else if (timedOut && !timeDisplay.Visible && Demon.CountLivingDemons(Scene) == 0)
            timeDisplay.Show($"+{Util.HundredthsToString((timeElapsedMillis - TimeLimitMillis) / 10)}");
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);

        if (Demon.CountLivingDemons(scene) == 0)
            Activate();
    }

    public void StartTimer() {
        if (state is State.Failed or State.Warping)
            return;

        timeElapsed = 0d;
        nextBeepAt = TimeLimitMillis - 500 * Math.Min(TimeLimitMillis / 500, 3);
        startThisFrame = true;
    }

    public void DemonKilled() => demonKilledThisFrame = true;

    private void UpdateCrystalY() => crystal.Y = bloom.Y = -12f + sine.Value;

    private void Activate() {
        state = State.Active;
        back.Visible = true;
        effect.Visible = true;
        bloom.Alpha = 0.5f;
    }

    private void Fail() {
        state = State.Failed;
        back.Visible = false;
        effect.Visible = false;
        bloom.Alpha = 0.2f;
        Scene.Tracker.GetEntity<RushStartLine>()?.Deactivate();
        Util.PlaySound("event:/classic/sfx14", 2f);
        crystal.Color = Color.Red;
    }

    private void WarpPlayer(Player player) {
        state = State.Warping;

        Strawberry golden = null;

        foreach (var follower in player.Leader.Followers) {
            if (follower.Entity is not Strawberry strawberry || !strawberry.Golden || strawberry.Winged)
                continue;

            golden = strawberry;

            break;
        }

        string nextLevel;
        var level = SceneAs<Level>();

        if (golden != null && !string.IsNullOrWhiteSpace(warpToWithGolden))
            nextLevel = warpToWithGolden;
        else if (!string.IsNullOrWhiteSpace(warpTo))
            nextLevel = warpTo;
        else
            nextLevel = level.GetNextLevel();

        if (!level.HasLevel(nextLevel))
            nextLevel = level.Session.Level;

        player.WarpToLevel(nextLevel);
    }
}