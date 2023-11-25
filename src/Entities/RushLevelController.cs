using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper; 

[CustomEntity("rushHelper/rushLevelController"), Tracked]
public class RushLevelController : Entity {
    private int remainingDemonCount;
    private bool demonKilledThisFrame;

    public RushLevelController(EntityData data, Vector2 offset) : base(data.Position + offset) => Tag = Tags.FrozenUpdate;

    public override void Awake(Scene scene) {
        base.Awake(scene);
        remainingDemonCount = Scene.Tracker.CountEntities<Demon>();
    }

    public void DemonKilled() {
        if (remainingDemonCount == 0)
            return;

        if (!demonKilledThisFrame) {
            demonKilledThisFrame = true;
            ((Level) Scene).OnEndOfFrame += () => {
                if (remainingDemonCount == 0)
                    Util.PlaySound("event:/classic/sfx13", 2f);
                else
                    Util.PlaySound("event:/classic/sfx8", 2f);
                
                demonKilledThisFrame = false;
            };
        }

        remainingDemonCount--;

        if (remainingDemonCount == 0)
            Scene.Tracker.GetEntity<RushGoal>()?.Activate();
    }
}