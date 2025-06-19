using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

[Tracked]
public class LevelClearedTimeRemainingDisplay : Entity {
    private readonly string text;

    public LevelClearedTimeRemainingDisplay(string text) {
        Tag = Tags.HUD | Tags.Global;
        this.text = text;
    }

    public override void Awake(Scene scene) {
        base.Awake(scene);

        Add(new Coroutine(RemoveAfterSecond()));
    }

    public override void Render() {
        base.Render();

        if (Scene.Paused)
            return;

        ActiveFont.DrawOutline(text, new Vector2(961f, 541f), new Vector2(0.5f, 0.5f), Vector2.One, Color.LimeGreen, 1f, Color.Black);
    }

    private IEnumerator RemoveAfterSecond() {
        yield return 1f;

        RemoveSelf();
    }
}