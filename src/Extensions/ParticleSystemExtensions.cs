using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper; 

public static class ParticleSystemExtensions {
    public static void Emit(this ParticleSystem particleSystem, ParticleBurst burst, Vector2 position, float angle)
        => particleSystem.Emit(burst.ParticleType, burst.Amount, position + burst.Offset, burst.Range, angle);
}