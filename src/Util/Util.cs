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

    public static IEnumerator NextFrame(Action action) {
        action();

        yield break;
    }

    public static IEnumerator AfterFrame(Action action) {
        yield return null;

        action();
    }

    public static IEnumerator AfterTime(float time, Action action) {
        yield return time;

        action();
    }

    public static void EmitCall(this ILCursor cursor, Delegate d) => cursor.Emit(OpCodes.Call, d.Method);

    public static ILHook CreateHook(this Type type, string name, ILContext.Manipulator manipulator)
        => new(type.GetMethod(name, ALL_FLAGS), manipulator);

    public static MethodInfo GetMethodUnconstrained(this Type type, string name) => type.GetMethod(name, ALL_FLAGS);

    public static PropertyInfo GetPropertyUnconstrained(this Type type, string name) => type.GetProperty(name, ALL_FLAGS);
}