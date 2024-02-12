using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper; 

[CustomEntity("rushHelper/surfPlatform"), Tracked]
public class SurfPlatform : Solid {
    private DynamicWaterSurface waterSurface;
    private Color color;

    public SurfPlatform(EntityData data, Vector2 offset) : base(data.Position + offset, data.Width, data.Height, true) {
        color = data.HexColorSafe("color", Color.LightSkyBlue);
        waterSurface = new DynamicWaterSurface(Position, (int) Width, 4, color * 0.3f, color * 0.8f, 360f, 1300f, 1.25f);
        SurfaceSoundIndex = 0;
        Depth = data.Bool("foreground") ? -19999 : -9999;
        
        Add(new DisplacementRenderHook(RenderDisplacement));
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
        Draw.Rect(X, Y + 4f, Width, Height - 4f, color * 0.3f);
        GameplayRenderer.End();
        waterSurface.Render(((Level) Scene).Camera);
        GameplayRenderer.Begin();
    }
    
    private void RenderDisplacement() => Draw.Rect(X, Y, Width, Height, new Color(0.5f, 0.5f, 0.25f, 1f));
}