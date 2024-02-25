using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

public static class LevelExtensions {
    public static void WarpToNextLevel(this Level level) {
        level.OnEndOfFrame += () => {
            var player = level.Tracker.GetEntity<Player>();
            var facing = player.Facing;

            player.CleanUpTriggers();
            level.TeleportTo(player, level.GetNextLevel(), Player.IntroTypes.Transition);
            level.Session.FirstLevel = false;
            level.Camera.Position = level.GetFullCameraTargetAt(player, player.Position);
            player.StateMachine.State = 0;
            player.Speed = Vector2.Zero;
            player.Dashes = 1;
            player.Facing = facing;
            player.Sprite.Scale = Vector2.One;
            player.ClearRushData();

            var tween = Tween.Create(Tween.TweenMode.Oneshot, null, 0.1f, true);

            tween.OnUpdate = tween => Glitch.Value = 0.5f * (1f - tween.Eased);
            player.Add(tween);
        };
    }

    private static string GetNextLevel(this Level level) {
        var session = level.Session;
        var levels = session.MapData.Levels;

        for (int i = levels.IndexOf(session.LevelData) + 1; i < levels.Count; i++) {
            if (levels[i].Spawns.Count > 0)
                return levels[i].Name;
        }

        return session.Level;
    }
}