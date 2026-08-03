using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomEntity("rushHelper/rushStartLine"), Tracked]
public class RushStartLine : Entity {
    private readonly Sprite[] sprites;
    private readonly TimeDisplay timeDisplay;

    public RushStartLine(EntityData data, Vector2 offset) : base(data.Position + offset) {
        int height = data.Height;

        sprites = new Sprite[height / 8];

        for (int i = 0; i < sprites.Length; i++) {
            var sprite = new Sprite(GFX.Game, "objects/rushHelper/rushStartLine/idle");

            sprite.AddLoop("idle", "", 0.25f);
            sprite.Play("idle");
            sprite.Color = Color.White * 0.5f;
            sprite.Position = new Vector2(0f, 8f * i);
            Add(sprite);
            sprites[i] = sprite;
        }

        Collider = new Hitbox(8f, height);
        Add(new PlayerCollider(OnPlayer));

        timeDisplay = new TimeDisplay(Color.LimeGreen);
    }

    public override void Added(Scene scene) {
        base.Added(scene);

        timeDisplay.Position = Center;
        scene.Add(timeDisplay);
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);

        var goal = scene.Tracker.GetEntity<RushGoal>();

        if (goal != null)
            timeDisplay.Show(Util.HundredthsToString(goal.TimeLimitMillis / 10));
        else
            timeDisplay.Visible = false;
    }

    public override void Removed(Scene scene) {
        base.Removed(scene);

        scene.Remove(timeDisplay);
    }

    public void Deactivate() {
        Collidable = false;
        timeDisplay.Visible = false;

        foreach (var sprite in sprites)
            sprite.Color = Color.White * 0.1f;
    }

    private void OnPlayer(Player player) {
        Scene.Tracker.GetEntity<RushGoal>()?.StartTimer();
        Scene.Tracker.GetEntity<RushLevelTitle>()?.FadeOut();
        Scene.Tracker.GetEntity<LevelClearedTimeRemainingDisplay>()?.RemoveSelf();
        Util.PlaySound("event:/classic/sfx4", 2f, Center);
        Deactivate();
    }
}