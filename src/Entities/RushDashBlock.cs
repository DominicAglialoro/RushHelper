using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using MonoMod;
using Monocle;

namespace Celeste.Mod.RushHelper;

[CustomEntity("rushHelper/rushDashBlock"), TrackedAs(typeof(DashBlock))]
public class RushDashBlock : DashBlock {
    public RushDashBlock(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) { }

    public override void Removed(Scene scene) {
        DestroyStaticMovers();
        Entity_Removed(scene);
    }

    [MonoModLinkTo("Monocle.Entity", "System.Void Removed(Monocle.Scene)")]
    private void Entity_Removed(Scene scene) => base.Removed(scene);
}