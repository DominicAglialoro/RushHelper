using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.RushHelper; 

[CustomEntity("rushHelper/demon"), Tracked]
public class Demon : Entity {
    private const float REMOVE_AFTER_KILL_TIME = 0.033f;

    private static readonly ParticleBurst[] KILL_PARTICLES_LARGE = {
        CreateKillParticleLarge(0, new Vector2(-4f, 0f)),
        CreateKillParticleLarge(1, new Vector2(1f, 4f)),
        CreateKillParticleLarge(2, new Vector2(2f, 2f))
    };
    
    private static readonly ParticleBurst KILL_PARTICLE_SMALL = new(new ParticleType {
        Source = GFX.Game["particles/triangle"],
        Color = Color.White,
        Color2 = Color.Black,
        ColorMode = ParticleType.ColorModes.Fade,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.2f,
        LifeMax = 0.3f,
        Size = 1f,
        DirectionRange = 0.78f,
        SpeedMin = 20f,
        SpeedMax = 80f,
        SpeedMultiplier = 0.005f,
        RotationMode = ParticleType.RotationModes.Random,
        SpinMin = 1.5707964f,
        SpinMax = 4.712389f,
        SpinFlippedChance = true
    }) {
        Amount = 8,
        Range = 6f * Vector2.One
    };

    private static ParticleBurst CreateKillParticleLarge(int index, Vector2 offset) => new(new ParticleType {
        Source = GFX.Game[$"particles/rushHelper/demonShatter/shatter{index}"],
        Color = Color.White,
        Color2 = Color.Black,
        ColorMode = ParticleType.ColorModes.Fade,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.5f,
        LifeMax = 0.8f,
        Size = 1f,
        DirectionRange = 0.78f,
        SpeedMin = 140f,
        SpeedMax = 210f,
        SpeedMultiplier = 0.005f,
        SpinMin = 1.5707964f,
        SpinMax = 4.712389f,
        SpinFlippedChance = true
    }) { Offset = offset };

    public static int KillInRadius(Scene scene, Vector2 center, float radius) {
        int killedCount = 0;
        var sum = Vector2.Zero;
        int dashRestores = 0;
            
        foreach (var entity in scene.Tracker.GetEntities<Demon>()) {
            var demon = (Demon) entity;
            
            if (!demon.alive || Vector2.DistanceSquared(center, demon.Center) > radius * radius)
                continue;
            
            float angle = (demon.Center - center).Angle();

            demon.Die(false);
            demon.Add(new Coroutine(Util.AfterFrame(() => demon.SpawnKillParticles(angle))));
            killedCount++;
            sum += demon.Center;

            if (demon.dashRestores > dashRestores)
                dashRestores = demon.dashRestores;
        }

        if (killedCount == 0)
            return 0;
        
        Audio.Play(SFX.game_09_iceball_break, sum / killedCount);
        scene.Tracker.GetEntity<RushLevelController>()?.DemonsKilled(killedCount);

        return dashRestores;
    }

    private int dashRestores;
    private Sprite body;
    private Image outline;
    private Image eyes;
    private Image feet;
    private SineWave sine;
    private Level level;
    private bool alive = true;

    public Demon(EntityData data, Vector2 offset) : base(data.Position + offset) {
        dashRestores = data.Int("dashRestores");

        Collider = new Circle(10f);
        Depth = 100;
        
        Add(body = new Sprite(GFX.Game, "objects/rushHelper/demon/body"));
        body.AddLoop("body", "", 0.1f);
        body.Play("body");
        body.CenterOrigin();
        
        Add(outline = new Image(GFX.Game["objects/rushHelper/demon/outline"]));
        outline.CenterOrigin();
        outline.Color = dashRestores switch {
            0 => Color.Cyan,
            1 => Color.White,
            2 => Color.HotPink,
            _ => Color.Violet
        };
        
        Add(eyes = new Image(GFX.Game["objects/rushHelper/demon/eyes"]));
        eyes.CenterOrigin();

        if (data.Bool("grounded")) {
            feet = new Image(GFX.Game["objects/rushHelper/demon/feet"]);
            
            Add(feet);
            feet.CenterOrigin();
            feet.Color = outline.Color;
        }
        
        Add(sine = new SineWave(0.6f));
        sine.Randomize();
        
        Add(new VertexLight(Color.White, 1f, 32, 64));
        Add(new PlayerCollider(OnPlayer));

        UpdateVisual();
    }
    
    public override void Added(Scene scene) {
        base.Added(scene);
        level = scene as Level;
    }

    public override void Update() {
        base.Update();
        UpdateVisual();
    }

    public void OnPlayer(Player player) {
        if (alive && player.HitDemon()) {
            Celeste.Freeze(0.016f);
            Audio.Play(SFX.game_09_iceball_break, Center);
            
            if (dashRestores >= 2)
                Audio.Play(SFX.game_10_pinkdiamond_touch, player.Position);
            
            Die(true);
            Add(new Coroutine(Util.AfterFrame(() => {
                var speed = player.Speed;

                if (speed == Vector2.Zero)
                    speed = player.DashDir;

                if (speed == Vector2.Zero)
                    SpawnKillParticles(player.Facing == Facings.Right ? 0f : MathHelper.Pi);
                else
                    SpawnKillParticles(speed.Angle());
            })));
            Scene.Tracker.GetEntity<RushLevelController>()?.DemonsKilled(1);
        }
        
        if (Collidable && !alive && player.RefillDashes(dashRestores))
            Collidable = false;
    }

    private void UpdateVisual() {
        body.Y = outline.Y = sine.Value;

        var player = Scene?.Tracker.GetEntity<Player>();

        if (player == null)
            return;
        
        var eyesOffset = new Vector2(0f, sine.Value - 1f);
        
        eyes.Position = eyesOffset + (player.Position - (Position + eyesOffset)).SafeNormalize().Round();
    }

    private void Die(bool allowPhantomRestore) {
        alive = false;

        if (dashRestores == 0 || !allowPhantomRestore)
            Collidable = false;

        body.Stop();
        body.Texture = GFX.Game["objects/rushHelper/demon/shatter"];
        outline.Visible = false;

        if (feet != null)
            feet.Visible = false;

        Add(new Coroutine(Util.AfterTime(REMOVE_AFTER_KILL_TIME, RemoveSelf)));
    }

    private void SpawnKillParticles(float angle) {
        Visible = false;
        level.ParticlesFG.Emit(KILL_PARTICLES_LARGE[0], Position, angle);
        level.ParticlesFG.Emit(KILL_PARTICLES_LARGE[1], Position, angle);
        level.ParticlesFG.Emit(KILL_PARTICLES_LARGE[2], Position, angle);
        level.ParticlesFG.Emit(KILL_PARTICLE_SMALL, Position, angle);
    }
}