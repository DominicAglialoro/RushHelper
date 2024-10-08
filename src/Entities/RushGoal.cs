using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomEntity("rushHelper/rushGoal"), Tracked]
public class RushGoal : Entity {
    private Image back;
    private Sprite crystal;
    private Sprite effect;
    private SineWave sine;
    private BloomPoint bloom;
    private bool warping;

    public RushGoal(EntityData data, Vector2 offset) : base(data.Position + offset) {
        Collider = new Hitbox(16f, 24f, -8f, -24f);
        Depth = 100;

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
        Add(bloom = new BloomPoint(0.5f, 16f));
        Add(new PlayerCollider(OnPlayer));

        Tag = Tags.FrozenUpdate;
        UpdateCrystalY();
    }

    public override void Update() {
        base.Update();
        UpdateCrystalY();
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);

        if (Scene.Tracker.GetEntity<Demon>() == null || Scene.Tracker.GetEntity<RushLevelController>() == null)
            return;

        Collidable = false;
        back.Visible = false;
        effect.Visible = false;
    }

    public void Activate() {
        Collidable = true;
        back.Visible = true;
        effect.Visible = true;
    }

    private void OnPlayer(Player player) {
        if (warping)
            return;

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

    private void UpdateCrystalY() => crystal.Y = bloom.Y = -12f + sine.Value;

    private void WarpToNextLevel(Player player) {
        var level = SceneAs<Level>();

        level.OnEndOfFrame += () => {
            player.CleanUpTriggers();
            level.TeleportTo(player, level.GetNextLevel(), Player.IntroTypes.Transition);
            level.Session.FirstLevel = false;
            level.Camera.Position = level.GetFullCameraTargetAt(player, player.Position);

            player.ResetStateValues();
            player.Facing = player.CollideFirst<SpawnFacingTrigger>()?.Facing ?? Facings.Right;

            var tween = Tween.Create(Tween.TweenMode.Oneshot, null, 0.1f, true);

            tween.OnUpdate = tween => Glitch.Value = 0.5f * (1f - tween.Eased);
            player.Add(tween);
        };
    }
}