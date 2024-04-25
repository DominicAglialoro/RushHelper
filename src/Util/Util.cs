using System;
using System.Collections;
using System.Reflection;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.RushHelper;

public static class Util {
    private const BindingFlags ALL_FLAGS = BindingFlags.Instance |
                                           BindingFlags.Static |
                                           BindingFlags.Public |
                                           BindingFlags.NonPublic;

    public static EventInstance PlaySound(string name, float volume = 1f, Vector2? position = null) {
        var instance = Audio.CreateInstance(name, position);

        if (instance == null)
            return null;

        instance.setVolume(volume);
        instance.start();
        instance.release();

        return instance;
    }

    public static Vector2 PreserveArea(Vector2 vec, float area = 1f) => area / (vec.X * vec.Y) * vec;

    public static void SetQuad(this VertexPositionColor[] mesh, int index, Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
        mesh[index].Position = a;
        mesh[index + 1].Position = b;
        mesh[index + 2].Position = c;
        mesh[index + 3].Position = b;
        mesh[index + 4].Position = c;
        mesh[index + 5].Position = d;
    }

    public static Color HexColorSafe(this EntityData data, string key, Color defaultColor = default)
        => data.Values != null ? data.HexColor(key, defaultColor) : defaultColor;

    public static Color HexToColorWithAlpha(string hex)
    {
        int num = 0;

        if (hex.Length >= 1 && hex[0] == '#')
            num = 1;

        switch (hex.Length - num) {
            case 6: {
                int r2 = Calc.HexToByte(hex[num++]) * 16 + Calc.HexToByte(hex[num++]);
                int g = Calc.HexToByte(hex[num++]) * 16 + Calc.HexToByte(hex[num++]);
                int b = Calc.HexToByte(hex[num++]) * 16 + Calc.HexToByte(hex[num]);

                return new Color(r2, g, b);
            }
            case 8: {
                int r = Calc.HexToByte(hex[num++]) * 16 + Calc.HexToByte(hex[num++]);
                int g = Calc.HexToByte(hex[num++]) * 16 + Calc.HexToByte(hex[num++]);
                int b = Calc.HexToByte(hex[num++]) * 16 + Calc.HexToByte(hex[num++]);
                int alpha = Calc.HexToByte(hex[num++]) * 16 + Calc.HexToByte(hex[num]);

                return new Color(r, g, b) * (alpha / 255f);
            }
            default:
                return Color.White;
        }
    }

    public static IEnumerator AfterFrame(Action action) {
        yield return null;

        action();
    }

    public static void EmitCall(this ILCursor cursor, Delegate d) => cursor.Emit(OpCodes.Call, d.Method);

    public static ILHook CreateHook(this Type type, string name, ILContext.Manipulator manipulator)
        => new(type.GetMethod(name, ALL_FLAGS), manipulator);

    public static int AddState(this StateMachine stateMachine, Func<int> onUpdate = null, Func<IEnumerator> coroutine = null, Action begin = null, Action end = null) {
        var dynamicData = DynamicData.For(stateMachine);
        var updates = dynamicData.Get<Func<int>[]>("updates");
        var coroutines = dynamicData.Get<Func<IEnumerator>[]>("coroutines");
        var begins = dynamicData.Get<Action[]>("begins");
        var ends = dynamicData.Get<Action[]>("ends");
        int nextIndex = begins.Length;

        Array.Resize(ref updates, begins.Length + 1);
        Array.Resize(ref coroutines, coroutines.Length + 1);
        Array.Resize(ref begins, begins.Length + 1);
        Array.Resize(ref ends, begins.Length + 1);

        dynamicData.Set("updates", updates);
        dynamicData.Set("coroutines", coroutines);
        dynamicData.Set("begins", begins);
        dynamicData.Set("ends", ends);
        stateMachine.SetCallbacks(nextIndex, onUpdate, coroutine, begin, end);

        return nextIndex;
    }

    public static void Emit(this ParticleSystem particleSystem, ParticleBurst burst, Vector2 position, float angle)
        => particleSystem.Emit(burst.ParticleType, burst.Amount, position + burst.Offset, burst.Range, angle);

    public static string GetNextLevel(this Level level) {
        var session = level.Session;
        var levels = session.MapData.Levels;

        for (int i = levels.IndexOf(session.LevelData) + 1; i < levels.Count; i++) {
            if (levels[i].Spawns.Count > 0)
                return levels[i].Name;
        }

        return session.Level;
    }
}