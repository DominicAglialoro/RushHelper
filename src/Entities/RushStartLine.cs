using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomEntity("rushHelper/rushStartLine"), Tracked]
public class RushStartLine : Entity {
    private readonly Sprite[] sprites;

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
    }

    public void Deactivate() {
        Collidable = false;

        foreach (var sprite in sprites)
            sprite.Color = Color.White * 0.1f;
    }

    private void OnPlayer(Player player) {
        Scene.Tracker.GetEntity<RushGoal>()?.StartTimer();
        Util.PlaySound("event:/classic/sfx4", 2f, Center);
        Deactivate();
    }
}