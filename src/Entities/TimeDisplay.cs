using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper;

public class TimeDisplay : Entity {
    private readonly Color color;

    private string text;

    public TimeDisplay(Color color) {
        this.color = color;
        Tag = TagsExt.SubHUD;
    }

    public override void Render() {
        var goal = Scene.Tracker.GetEntity<RushGoal>();

        if (Scene.Paused || goal == null)
            return;

        var cameraPosition = SceneAs<Level>().Camera.Position;
        var drawPosition = 6f * (Position - cameraPosition);

        ActiveFont.DrawOutline(text, drawPosition, new Vector2(0.5f, 0.5f), 0.75f * Vector2.One, color, 1f, Color.Black);
    }

    public void Show(string text) {
        this.text = text;
        Visible = true;
    }
}