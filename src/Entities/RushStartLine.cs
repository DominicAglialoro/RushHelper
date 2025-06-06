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

        timeDisplay = new TimeDisplay();
    }

    public override void Added(Scene scene) {
        base.Added(scene);

        timeDisplay.Position = Center;
        timeDisplay.Visible = true;
        scene.Add(timeDisplay);
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
        Util.PlaySound("event:/classic/sfx4", 2f, Center);
        Deactivate();
    }

    private class TimeDisplay : Entity {
        public TimeDisplay() => Tag = Tags.HUD;

        public override void Render() {
            var goal = Scene.Tracker.GetEntity<RushGoal>();

            if (Scene.Paused || goal == null)
                return;

            var cameraPosition = SceneAs<Level>().Camera.Position;
            var drawPosition = 6f * (Position - cameraPosition);

            ActiveFont.DrawOutline(goal.TimeLimit.ToString("F"), drawPosition, new Vector2(0.5f, 0.5f), 0.75f * Vector2.One, Color.LimeGreen, 1f, Color.Black);
        }
    }
}