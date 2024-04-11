using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.RushHelper;

[CustomEntity(
     "rushHelper/rushSpikesUp = LoadUp",
     "rushHelper/rushSpikesDown = LoadDown",
     "rushHelper/rushSpikesLeft = LoadLeft",
     "rushHelper/rushSpikesRight = LoadRight"), TrackedAs(typeof(Spikes))]
public class RushSpikes : Spikes {
    public static Entity LoadUp(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
        => new RushSpikes(entityData, offset, Directions.Up);

    public static Entity LoadDown(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
        => new RushSpikes(entityData, offset, Directions.Down);

    public static Entity LoadLeft(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
        => new RushSpikes(entityData, offset, Directions.Left);

    public static Entity LoadRight(Level level, LevelData levelData, Vector2 offset, EntityData entityData)
        => new RushSpikes(entityData, offset, Directions.Right);

    public RushSpikes(EntityData data, Vector2 offset, Directions dir) : base(data, offset, dir) {
        var dynamicData = DynamicData.For(this);

        Remove(dynamicData.Get<PlayerCollider>("pc"));

        var pc = new PlayerCollider(OnCollide);

        Add(pc);
        dynamicData.Set("pc", pc);
    }

    private void OnCollide(Player player) {
        switch (Direction) {
            case Directions.Up:
                if (player.Speed.Y < 0.0 || player.Bottom > Bottom || player.IsInDestroyBlockState() && player.Speed.Y > 0)
                    break;

                player.Die(new Vector2(0.0f, -1f));
                break;
            case Directions.Down:
                if (player.Speed.Y > 0.0 || player.IsInDestroyBlockState() && player.Speed.Y < 0)
                    break;

                player.Die(new Vector2(0.0f, 1f));
                break;
            case Directions.Left:
                if (player.Speed.X < 0.0 || player.IsInDestroyBlockState() && player.Speed.X > 0)
                    break;

                player.Die(new Vector2(-1f, 0.0f));
                break;
            case Directions.Right:
                if (player.Speed.X > 0.0 || player.IsInDestroyBlockState() && player.Speed.X < 0)
                    break;

                player.Die(new Vector2(1f, 0.0f));
                break;
        }
    }
}