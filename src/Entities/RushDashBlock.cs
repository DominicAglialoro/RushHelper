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
        base_Removed(scene);
    }

    [MonoModLinkTo("Celeste.Solid", "System.Void Removed(Monocle.Scene)")]
    private void base_Removed(Scene scene) => base.Removed(scene);
}