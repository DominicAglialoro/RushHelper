using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

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

    public static Hook CreateHook(this Type type, string name, Delegate method)
        => new(type.GetMethod(name, ALL_FLAGS), method);

    public static Hook CreateGetterHook(this Type type, string name, Delegate method)
        => new(type.GetProperty(name, ALL_FLAGS).GetGetMethod(), method);

    public static ILHook CreateILHook(this Type type, string name, ILContext.Manipulator manipulator)
        => new(type.GetMethod(name, ALL_FLAGS), manipulator);

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

    public static bool HasLevel(this Level level, string name) {
        foreach (var levelData in level.Session.MapData.Levels) {
            if (levelData.Name == name)
                return true;
        }

        return false;
    }

    public static string HundredthsToString(int hundredths) => hundredths > 0 ? $"{hundredths / 100}.{(hundredths % 100):00}" : "0.00";
}