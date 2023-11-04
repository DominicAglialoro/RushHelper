using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper; 

[CustomEntity("rushHelper/surfPlatform"), Tracked]
public class SurfPlatform : Solid {
    private DynamicWaterSurface waterSurface;

    public SurfPlatform(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, data.Height, true) {
        waterSurface = new DynamicWaterSurface(Position - Vector2.UnitY, (int) Width, 5, 360f, 1300f, 1.25f);
        SurfaceSoundIndex = 0;
        Depth = -9999;
    }

    public override void Update() {
        base.Update();

        var player = GetPlayerOnTop();

        if (player != null && player.Speed.X != 0f)
            waterSurface.ApplyForce(player.Position.X, 7.5f * Math.Abs(player.Speed.X) * Engine.DeltaTime, 2);

        int cameraLeft = (int) ((Level) Scene).Camera.Position.X;
        
        waterSurface.Update(cameraLeft, cameraLeft + 320, Engine.DeltaTime);
    }

    public override void Render() {
        Draw.Rect(X, Y + 4f, Width, Height - 4f, Water.FillColor);
        GameplayRenderer.End();
        waterSurface.Render(((Level) Scene).Camera);
        GameplayRenderer.Begin();
    }
}