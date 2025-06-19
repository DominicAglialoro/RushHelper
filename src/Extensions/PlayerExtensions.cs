using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.RushHelper;

public static class PlayerExtensions {
    private const int MAX_CARD_COUNT = 3;
    private const float YELLOW_MIN_X = 90f;
    private const float YELLOW_ADD_X = 40f;
    private const float YELLOW_Y = -160f;
    private const float YELLOW_VAR_JUMP_TIME = 0.25f;
    private const float BLUE_SPEED = 720f;
    private const float BLUE_END_SPEED = 240f;
    private const float BLUE_MAX_END_SPEED = 325f;
    private const float BLUE_DURATION = 0.15f;
    private const float BLUE_ALLOW_JUMP_AT = 0.05f;
    private const float BLUE_HYPER_GRACE_TIME_GROUND = 0.05f;
    private const float BLUE_HYPER_GRACE_TIME_DEMON = 0.1f;
    private const float GREEN_FALL_SPEED = 360f;
    private const float GREEN_LAND_SPEED = 90f;
    private const float GREEN_LAND_KILL_RADIUS = 40f;
    private const float RED_DASH_SPEED = 240f;
    private const float RED_DASH_DURATION = 0.15f;
    private const float RED_DASH_ATTACK = 0.3f;
    private const float RED_BOOST_DURATION = 1f;
    private const float RED_ACCEL_SPEED = 240f;
    private const float RED_ACCEL_ACCELERATION = 2000f;
    private const float RED_BOUNCE_ADD_SPEED = 40f;
    private const float RED_LATE_BOUNCE_TIME = 0.1f;
    private const float RED_WALL_SPEED_RETENTION_TIME = 0.1f;
    private const float WHITE_SPEED = 240f;
    private const float WHITE_ACCELERATE_MULT = 1.2f;
    private const float WHITE_JUMP_GRACE_TIME = 0.1f;

    private static readonly ParticleType RED_PARTICLE = new() {
        Color = Color.Red,
        Color2 = Color.Orange,
        ColorMode = ParticleType.ColorModes.Choose,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.1f,
        LifeMax = 0.3f,
        Size = 1f,
        SpeedMin = 10f,
        SpeedMax = 20f,
        DirectionRange = MathHelper.TwoPi
    };

    private static readonly List<Hook> hooks = new();
    private static readonly List<ILHook> ilHooks = new();

    public static void Load() {
        On.Celeste.Player.Update += Player_Update;
        On.Celeste.Player.UpdateSprite += Player_UpdateSprite;
        IL.Celeste.Player.BeforeDownTransition += Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition += Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.WallJump += Player_WallJump;
        On.Celeste.Player.Rebound += Player_Rebound;
        On.Celeste.Player.Die += Player_Die;
        IL.Celeste.Player.OnCollideH += Player_OnCollideH_il;
        IL.Celeste.Player.OnCollideV += Player_OnCollideV_il;
        On.Celeste.Player.OnBoundsH += Player_OnBoundsH;
        On.Celeste.Player.OnBoundsV += Player_OnBoundsV;
        On.Celeste.Player.NormalUpdate += Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate += Player_NormalUpdate_il;
        IL.Celeste.Player.ClimbUpdate += InsertUseCard;
        IL.Celeste.Player.SwimUpdate += InsertUseCard;
        IL.Celeste.Player.RedDashUpdate += InsertUseCard;
        IL.Celeste.Player.HitSquashUpdate += InsertUseCard;
        IL.Celeste.Player.LaunchUpdate += InsertUseCard;
        On.Celeste.Player.DreamDashBegin += Player_DreamDashBegin;
        IL.Celeste.Player.StarFlyUpdate += InsertUseCard;

        hooks.Add(typeof(Player).CreateGetterHook(nameof(Player.DashAttacking), Player_get_DashAttacking));
        hooks.Add(typeof(Player).CreateHook(nameof(Player.DashBegin), InsertResetRedBoostTimer));
        hooks.Add(typeof(Player).CreateHook(nameof(Player.BoostBegin), InsertResetRedBoostTimer));
        hooks.Add(typeof(Player).CreateHook(nameof(Player.SummitLaunchBegin), InsertResetRedBoostTimer));
        hooks.Add(typeof(Player).CreateHook(nameof(Player.StarFlyBegin), InsertResetRedBoostTimer));
        ilHooks.Add(typeof(Player).CreateILHook(nameof(Player.orig_Update), Player_orig_Update_il));
    }

    public static void Unload() {
        On.Celeste.Player.Update -= Player_Update;
        On.Celeste.Player.UpdateSprite -= Player_UpdateSprite;
        IL.Celeste.Player.BeforeDownTransition -= Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition -= Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump -= Player_Jump;
        On.Celeste.Player.WallJump -= Player_WallJump;
        On.Celeste.Player.Rebound -= Player_Rebound;
        On.Celeste.Player.Die -= Player_Die;
        IL.Celeste.Player.OnCollideH -= Player_OnCollideH_il;
        IL.Celeste.Player.OnCollideV -= Player_OnCollideV_il;
        On.Celeste.Player.OnBoundsH -= Player_OnBoundsH;
        On.Celeste.Player.OnBoundsV -= Player_OnBoundsV;
        On.Celeste.Player.NormalUpdate -= Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate -= Player_NormalUpdate_il;
        IL.Celeste.Player.ClimbUpdate -= InsertUseCard;
        IL.Celeste.Player.SwimUpdate -= InsertUseCard;
        IL.Celeste.Player.RedDashUpdate -= InsertUseCard;
        IL.Celeste.Player.HitSquashUpdate -= InsertUseCard;
        IL.Celeste.Player.LaunchUpdate -= InsertUseCard;
        On.Celeste.Player.DreamDashBegin -= Player_DreamDashBegin;
        IL.Celeste.Player.StarFlyUpdate -= InsertUseCard;

        foreach (var hook in hooks)
            hook.Dispose();

        foreach (var hook in ilHooks)
            hook.Dispose();

        hooks.Clear();
        ilHooks.Clear();
    }

    public static void ResetStateValues(this Player player) {
        player.StateMachine.State = Player.StNormal;
        player.Speed = Vector2.Zero;
        player.Dashes = 1;
        player.Sprite.Scale = Vector2.One;
        player.AutoJump = false;
        player.AutoJumpTimer = 0f;
        player.dashAttackTimer = 0f;
        player.dashTrailTimer = 0f;
        player.dashTrailCounter = 0;
        player.forceMoveXTimer = 0f;
        player.gliderBoostTimer = 0f;
        player.jumpGraceTimer = 0f;
        player.launched = false;
        player.launchedTimer = 0f;
        player.varJumpSpeed = 0f;
        player.varJumpTimer = 0f;
        player.wallBoostDir = 0;
        player.wallBoostTimer = 0f;

        if (!player.TryGetData(out var rushData))
            return;

        var cards = rushData.Cards;

        cards.Clear();
        rushData.CardInventoryIndicator.UpdateInventory(cards);
        rushData.JustUsedCard = false;
        rushData.BlueHyperTimePassed = false;
        rushData.RedBoostTimer = 0f;
        rushData.RedLateBounceTimer = 0f;
        rushData.RedLateBounceSpeed = 0f;
        rushData.RedParticleEmitter.Active = false;
        rushData.WhiteRedirect = false;
        rushData.WhiteJumpSpeedReturn = 0f;
        player.Stop(rushData.WhiteSoundSource);
        player.Stop(rushData.RedSoundSource);
    }

    public static void RefillDashes(this Player player, int dashes) {
        if (dashes == 0)
            return;

        player.RefillStamina();
        player.RefillDash();

        if (dashes > player.Dashes)
            player.Dashes = dashes;
    }

    public static bool TryGiveCard(this Player player, AbilityCardType cardType) {
        var rushData = player.GetOrCreateData();
        var cards = rushData.Cards;

        if (cards.Count == MAX_CARD_COUNT)
            return false;

        cards.Enqueue(cardType);
        rushData.CardInventoryIndicator.UpdateInventory(cards);
        rushData.CardInventoryIndicator.PlayAnimation();

        return true;
    }

    public static bool HitDemon(this Player player) {
        if (!player.TryGetData(out var rushData))
            return player.DashAttacking || player.StateMachine.State == Player.StDash;

        int state = player.StateMachine.State;

        if (!player.DashAttacking
            && rushData.RedBoostTimer == 0f
            && state != Player.StDash
            && state != rushData.StBlue
            && state != rushData.StGreen
            && state != rushData.StWhite)
            return false;

        if (state == rushData.StBlue) {
            if (!rushData.JustUsedCard)
                player.jumpGraceTimer = BLUE_HYPER_GRACE_TIME_DEMON;
        }
        else if (state == rushData.StWhite) {
            if (rushData.JustUsedCard)
                return false;

            if (player.DashDir.X != 0f) {
                player.dreamJump = true;
                rushData.WhiteJumpSpeedReturn = Math.Sign(player.Speed.X) * player.Speed.Length() - player.Speed.X;

                if (Input.Jump.Pressed)
                    player.Jump();

                player.jumpGraceTimer = WHITE_JUMP_GRACE_TIME;
            }

            player.StateMachine.State = Player.StNormal;
            Celeste.Freeze(0.05f);
        }

        return true;
    }

    public static bool IsInCustomDash(this Player player) {
        if (!player.TryGetData(out var rushData))
            return false;

        int state = player.StateMachine.State;

        return state == rushData.StBlue
               || state == rushData.StGreen
               || state == rushData.StRed
               || state == rushData.StWhite;
    }

    private static RushData GetData(this Player player) => player.Components.Get<RushData>();

    private static RushData GetOrCreateData(this Player player) {
        var rushData = player.Components.Get<RushData>();

        if (rushData != null)
            return rushData;

        var stateMachine = player.StateMachine;
        int stYellow = stateMachine.AddState<Player>("RushYellow", player => player.StateMachine.State, YellowCoroutine, YellowBegin);
        int stBlue = stateMachine.AddState<Player>("RushBlue", BlueUpdate, BlueCoroutine, BlueBegin, BlueEnd);
        int stGreen = stateMachine.AddState<Player>("RushGreen", GreenUpdate, GreenCoroutine, GreenBegin);
        int stRed = stateMachine.AddState<Player>("RushRed", RedUpdate, RedCoroutine, RedBegin);
        int stWhite = stateMachine.AddState<Player>("RushWhite", WhiteUpdate, WhiteCoroutine, WhiteBegin, WhiteEnd);

        rushData = new RushData(stYellow, stBlue, stGreen, stRed, stWhite);
        player.Add(rushData.CardInventoryIndicator);
        player.Add(rushData.RedParticleEmitter);
        player.Add(rushData.RedSoundSource);
        player.Add(rushData.WhiteSoundSource);
        rushData.RedParticleEmitter.Active = false;

        player.Add(rushData);

        return rushData;
    }

    private static bool TryGetData(this Player player, out RushData rushData) {
        rushData = player.Components.Get<RushData>();

        return rushData != null;
    }

    private static bool NextCardIs(this Player player, AbilityCardType cardType) {
        if (!player.TryGetData(out var rushData))
            return false;

        var cards = rushData.Cards;

        return cards.Count > 0 && cards.Peek() == cardType;
    }

    private static bool CheckUseCard(this Player player) {
        if (!player.TryGetData(out var rushData)
            || rushData.Cards.Count == 0
            || !RushHelperModule.Settings.UseCard.Pressed)
            return false;

        RushHelperModule.Settings.UseCard.ConsumeBuffer();

        return true;
    }

    private static int UseCard(this Player player) {
        var rushData = player.GetData();

        rushData.JustUsedCard = true;

        return player.PopCard() switch {
            AbilityCardType.Yellow => rushData.StYellow,
            AbilityCardType.Blue => rushData.StBlue,
            AbilityCardType.Green => rushData.StGreen,
            AbilityCardType.Red => rushData.StRed,
            AbilityCardType.White => rushData.StWhite,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static AbilityCardType PopCard(this Player player) {
        var rushData = player.GetData();
        var cards = rushData.Cards;
        var cardInventoryIndicator = rushData.CardInventoryIndicator;
        var cardType = cards.Dequeue();

        cardInventoryIndicator.UpdateInventory(cards);
        cardInventoryIndicator.StopAnimation();

        foreach (UseCardListener listener in player.Scene.Tracker.GetComponents<UseCardListener>())
            listener.OnUseCard?.Invoke(cardType);

        return cardType;
    }

    private static void PrepareForCustomDash(this Player player) {
        var rushData = player.GetData();

        player.dashAttackTimer = 0f;
        player.forceMoveXTimer = 0f;
        player.gliderBoostTimer = 0f;
        player.launched = false;
        player.wallSlideTimer = 1.2f;

        player.beforeDashSpeed = player.Speed;
        player.dashStartedOnGround = player.onGround;
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;

        rushData.RedBoostTimer = 0f;
        player.dashTrailTimer = 0.016f;
        Celeste.Freeze(0.05f);
    }

    private static void DoGreenSlam(this Player player) {
        var rushData = player.GetData();

        player.Speed.X = player.moveX * GREEN_LAND_SPEED;
        player.Sprite.Scale = new Vector2(1.5f, 0.75f);
        player.Play(SFX.game_gen_fallblock_impact);
        Celeste.Freeze(0.05f);
        player.StateMachine.State = Player.StNormal;

        var level = player.SceneAs<Level>();

        level.Particles.Emit(Player.P_SummitLandA, 12, player.BottomCenter, Vector2.UnitX * 3f, -1.5707964f);
        level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter - Vector2.UnitX * 2f, Vector2.UnitX * 2f, 3.403392f);
        level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter + Vector2.UnitX * 2f, Vector2.UnitX * 2f, -0.2617994f);
        level.Displacement.AddBurst(player.Center, 0.4f, 16f, 128f, 1f, Ease.QuadOut, Ease.QuadOut);

        var collider = player.Collider;

        player.Collider = rushData.GreenKillCircle;

        foreach (Demon demon in player.CollideAll<Demon>())
            demon.Slammed(player);

        foreach (DashBlock dashBlock in player.CollideAll<DashBlock>())
            dashBlock.Break(player.Center, Vector2.Zero, true, true);

        foreach (TempleCrackedBlock templeCrackedBlock in player.CollideAll<TempleCrackedBlock>())
            templeCrackedBlock.Break(player.Center);

        player.Collider = collider;
    }

    private static void DoWhiteDash(this Player player, Vector2 direction, float speed) {
        player.DashDir = direction;
        player.Speed = speed * direction;
        player.SceneAs<Level>().Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);

        if (direction.X == 0f) {
            player.Sprite.Scale = new Vector2(0.67f, 1.5f);
            player.Sprite.Rotation = 0f;
        }
        else {
            player.Sprite.Scale = new Vector2(1.5f, 0.67f);
            player.Sprite.Rotation = (Math.Sign(direction.X) * direction).Angle();
        }

        player.Sprite.Origin.Y = 26f;
        player.Sprite.Position.Y = -6f;
    }

    private static void UpdateTrail(this Player player, Color color, float duration) {
        float dashTrailTimer = player.dashTrailTimer - Engine.DeltaTime;

        if (dashTrailTimer > 0f) {
            player.dashTrailTimer = dashTrailTimer;

            return;
        }

        player.dashTrailTimer = 0.016f;
        TrailManager.Add(player.Position, player.Sprite, player.Hair.Visible ? player.Hair : null,
            new Vector2((float) player.Facing * Math.Abs(player.Sprite.Scale.X), player.Sprite.Scale.Y),
            color, player.Depth + 1, duration);
    }

    private static bool TryJump(this Player player) {
        if (!Input.Jump.Pressed || player.DashDir.Y != 0f || player.jumpGraceTimer <= 0f)
            return false;

        player.Jump();

        return true;
    }

    private static bool TryWallJump(this Player player) {
        if (!Input.Jump.Pressed || !player.CanUnDuck)
            return false;

        if (player.WallJumpCheck(1)) {
            if (player.Facing == Facings.Right && Input.GrabCheck && player.Stamina > 0f && player.Holding == null
                && !ClimbBlocker.Check(player.Scene, player, player.Position + Vector2.UnitX * 3f))
                player.ClimbJump();
            else
                player.WallJump(-1);
        }
        else if (player.WallJumpCheck(-1)) {
            if (player.Facing == Facings.Left && Input.GrabCheck && player.Stamina > 0f && player.Holding == null
                && !ClimbBlocker.Check(player.Scene, player, player.Position + Vector2.UnitX * -3f))
                player.ClimbJump();
            else
                player.WallJump(1);
        }
        else
            return false;

        return true;
    }

    private static bool TrySuperWallJump(this Player player) {
        if (!Input.Jump.Pressed || !player.CanUnDuck || player.DashDir.X != 0f || player.DashDir.Y >= 0f)
            return false;

        if (player.WallJumpCheck(1))
            player.SuperWallJump(-1);
        else if (player.WallJumpCheck(-1))
            player.SuperWallJump(1);
        else
            return false;

        return true;

    }

    private static bool TryGrabHoldable(this Player player) {
        if (!Input.GrabCheck || player.Holding != null || !player.CanUnDuck || player.IsTired)
            return false;

        foreach (Holdable holdable in player.Scene.Tracker.GetComponents<Holdable>()) {
            if (holdable.Check(player) && player.Pickup(holdable))
                return true;
        }

        return false;
    }

    private static void TryJumpThruCorrect(this Player player) {
        foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
            if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !player.DashCorrectCheck(Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                player.MoveVExact((int) (jumpThru.Top - player.Bottom));
        }
    }

    private static void YellowBegin(Player player) {
        player.dashAttackTimer = 0f;
        player.forceMoveXTimer = 0f;
        player.gliderBoostTimer = 0f;
        player.jumpGraceTimer = 0f;
        player.launched = false;
        player.varJumpTimer = 0f;
        player.wallBoostTimer = 0f;
        player.wallSlideTimer = 1.2f;

        player.beforeDashSpeed = player.Speed;
        player.Speed = Vector2.Zero;
        player.Sprite.Scale = new Vector2(0.67f, 1.5f);
        player.Scene.Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, MathHelper.PiOver2, Color.White));
        player.Play(SFX.game_gen_thing_booped);
        Celeste.Freeze(0.016f);
    }

    private static IEnumerator YellowCoroutine(Player player) {
        yield return null;

        var rushData = player.GetData();

        rushData.JustUsedCard = false;
        player.Speed = player.beforeDashSpeed;

        int moveX = Input.MoveX.Value;
        var liftBoost = player.LiftBoost;

        player.Speed.X += moveX * YELLOW_ADD_X + liftBoost.X;

        if (moveX != 0 && moveX * player.Speed.X < YELLOW_MIN_X)
            player.Speed.X = moveX * YELLOW_MIN_X;

        player.Speed.Y = YELLOW_Y + liftBoost.Y;
        player.AutoJump = true;
        player.AutoJumpTimer = 0f;
        player.varJumpSpeed = player.Speed.Y;
        player.varJumpTimer = YELLOW_VAR_JUMP_TIME;
        player.StateMachine.State = Player.StNormal;
    }

    private static void BlueBegin(Player player) {
        var rushData = player.GetData();

        player.PrepareForCustomDash();
        player.jumpGraceTimer = 0f;
        rushData.BlueHyperTimePassed = false;

        if (!player.onGround && player.Ducking && player.CanUnDuck)
            player.Ducking = false;

        player.Sprite.Play("dash");
        player.Play("event:/rushHelper/game/blue_dash");
    }

    private static int BlueUpdate(Player player) {
        var rushData = player.GetData();

        if (rushData.JustUsedCard)
            return rushData.StBlue;

        if (player.TryGrabHoldable())
            return Player.StPickup;

        player.TryJumpThruCorrect();

        if (Input.Jump.Pressed && player.CanUnDuck && rushData.BlueHyperTimePassed
            && player.jumpGraceTimer > 0f) {
            player.Ducking = true;
            player.StateMachine.State = Player.StNormal;
            player.SuperJump();

            return Player.StNormal;
        }

        if (player.TryWallJump())
            return Player.StNormal;

        player.UpdateTrail(Color.Blue, 0.66f);

        return rushData.StBlue;
    }

    private static IEnumerator BlueCoroutine(Player player) {
        yield return null;

        var rushData = player.GetData();

        rushData.JustUsedCard = false;

        int aimX = Math.Sign(player.CorrectDashPrecision(player.lastAim).X);

        if (aimX == 0)
            aimX = (int) player.Facing;

        player.DashDir.X = aimX;
        player.DashDir.Y = 0f;
        player.Speed.X = aimX * BLUE_SPEED;
        player.Speed.Y = 0f;
        player.Facing = (Facings) aimX;
        player.SceneAs<Level>().Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        SlashFx.Burst(player.Center, player.DashDir.Angle());

        for (float timer = 0f; timer < BLUE_DURATION; timer += Engine.DeltaTime) {
            rushData.BlueHyperTimePassed = timer >= BLUE_ALLOW_JUMP_AT;

            var stretch = Vector2.Lerp(new Vector2(2f, 0.5f), Vector2.One, timer / BLUE_DURATION);

            player.Sprite.Scale = stretch / (stretch.X * stretch.Y);

            yield return null;
        }

        player.Speed.X = player.DashDir.X * BLUE_END_SPEED;
        player.StateMachine.State = Player.StNormal;
    }

    private static void BlueEnd(Player player) {
        if (Math.Abs(player.Speed.X) > BLUE_MAX_END_SPEED)
            player.Speed.X = Math.Sign(player.Speed.X) * BLUE_MAX_END_SPEED;

        float wallSpeedRetained = player.wallSpeedRetained;

        if (Math.Sign(wallSpeedRetained) != Math.Sign(player.DashDir.X)) {
            player.wallSpeedRetained = 0f;
            player.wallSpeedRetentionTimer = 0f;
        }
        else if (Math.Abs(wallSpeedRetained) > BLUE_MAX_END_SPEED)
            player.wallSpeedRetained =  Math.Sign(player.wallSpeedRetained) * BLUE_MAX_END_SPEED;

        player.jumpGraceTimer = 0f;
        player.Sprite.Scale = Vector2.One;
    }

    private static void GreenBegin(Player player) {
        player.PrepareForCustomDash();
        player.varJumpTimer = 0f;

        if (!player.onGround && player.Ducking && player.CanUnDuck)
            player.Ducking = false;

        player.Sprite.Play("fallFast");
        player.Play(SFX.game_05_crackedwall_vanish);
    }

    private static int GreenUpdate(Player player) {
        var rushData = player.GetData();

        if (rushData.JustUsedCard)
            return rushData.StGreen;

        if (player.TryWallJump())
            return Player.StNormal;

        if (!player.NextCardIs(AbilityCardType.Green) && player.CheckUseCard()) {
            player.Sprite.Scale = Vector2.One;

            return player.UseCard();
        }

        if (player.CanDash) {
            player.Sprite.Scale = Vector2.One;

            return player.StartDash();
        }

        player.UpdateTrail(Color.Green, 0.33f);
        player.Sprite.Scale = new Vector2(0.56f, 1.8f);

        return rushData.StGreen;
    }

    private static IEnumerator GreenCoroutine(Player player) {
        yield return null;

        var rushData = player.GetData();

        rushData.JustUsedCard = false;
        player.Speed = new Vector2(0f, GREEN_FALL_SPEED);
        player.DashDir = Vector2.UnitY;
    }

    private static void RedBegin(Player player) {
        player.Speed += player.LiftBoost;
        player.PrepareForCustomDash();
        player.dashAttackTimer = RED_DASH_ATTACK;

        if (!player.onGround && player.Ducking && player.CanUnDuck)
            player.Ducking = false;

        player.Sprite.Play("dash");
        player.Play("event:/rushHelper/game/red_boost_dash");
    }

    private static int RedUpdate(Player player) {
        var rushData = player.GetData();

        if (rushData.JustUsedCard)
            return rushData.StRed;

        if (player.TryGrabHoldable())
            return Player.StPickup;

        if (player.TryJump() || player.TrySuperWallJump() || player.TryWallJump())
            return Player.StNormal;

        if (player.DashDir.Y == 0f)
            player.TryJumpThruCorrect();

        return rushData.StRed;
    }

    private static IEnumerator RedCoroutine(Player player) {
        yield return null;

        var rushData = player.GetData();

        rushData.JustUsedCard = false;
        player.SceneAs<Level>().Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        rushData.RedBoostTimer = RED_BOOST_DURATION;
        player.Loop(rushData.RedSoundSource, "event:/rushHelper/game/red_boost_sustain");
        rushData.RedSoundSource.DisposeOnTransition = false;

        var beforeDashSpeed = player.beforeDashSpeed;

        player.DashDir = player.CorrectDashPrecision(player.lastAim);
        player.Speed = RED_DASH_SPEED * player.DashDir;

        if (Math.Sign(player.Speed.X) == Math.Sign(beforeDashSpeed.X) && Math.Abs(beforeDashSpeed.X) > Math.Abs(player.Speed.X))
            player.Speed.X = beforeDashSpeed.X;

        if (player.onGround && player.DashDir.X != 0f && player.DashDir.Y > 0f) {
            player.DashDir.X = Math.Sign(player.DashDir.X);
            player.DashDir.Y = 0f;
            player.Ducking = true;
            player.Speed.X *= 1.2f;
            player.Speed.Y = 0f;
        }

        SlashFx.Burst(player.Center, player.DashDir.Angle());

        yield return RED_DASH_DURATION;

        player.StateMachine.State = Player.StNormal;
    }

    private static void WhiteBegin(Player player) {
        var rushData = player.GetData();

        player.Speed += player.LiftBoost;
        player.PrepareForCustomDash();
        player.varJumpTimer = 0f;

        if (player.Ducking && player.CanUnDuck)
            player.Ducking = false;

        player.Sprite.Scale = Vector2.One;
        player.Sprite.Play("dreamDashIn");
        player.Hair.Visible = false;
        player.Play(SFX.char_bad_dash_red_right);
        player.Loop(rushData.WhiteSoundSource, SFX.char_mad_dreamblock_travel);
        rushData.WhiteSoundSource.DisposeOnTransition = false;
    }

    private static int WhiteUpdate(Player player) {
        var rushData = player.GetData();

        if (rushData.JustUsedCard)
            return rushData.StWhite;

        if (rushData.WhiteRedirect) {
            var direction = player.CorrectDashPrecision(player.lastAim);
            float dashSpeed = player.beforeDashSpeed.Length();

            if (direction == player.DashDir)
                dashSpeed *= WHITE_ACCELERATE_MULT;

            if (dashSpeed < WHITE_SPEED)
                dashSpeed = WHITE_SPEED;

            player.DoWhiteDash(direction, dashSpeed);
            rushData.WhiteRedirect = false;

            return rushData.StWhite;
        }

        if (player.CheckUseCard()) {
            if (!player.NextCardIs(AbilityCardType.White))
                return player.UseCard();

            player.PopCard();
            player.beforeDashSpeed = player.Speed;
            player.Speed = Vector2.Zero;
            player.Sprite.Scale = Vector2.One;
            player.Sprite.Play("dreamDashIn");
            player.Sprite.SetAnimationFrame(2);
            player.Play(SFX.char_bad_dash_red_right);
            Celeste.Freeze(0.033f);
            rushData.WhiteRedirect = true;

            return rushData.StWhite;
        }

        if (player.CanDash)
            return player.StartDash();

        if (player.DashDir.Y == 0f)
            player.TryJumpThruCorrect();

        if (player.TryJump() || player.TrySuperWallJump() || player.TryWallJump())
            return Player.StNormal;

        player.UpdateTrail(Color.White, 0.33f);

        return rushData.StWhite;
    }

    private static IEnumerator WhiteCoroutine(Player player) {
        yield return null;

        var rushData = player.GetData();

        rushData.JustUsedCard = false;

        var direction = player.CorrectDashPrecision(player.lastAim);
        var beforeDashSpeed = player.beforeDashSpeed;
        float dashSpeed = WHITE_SPEED;

        if (direction.X != 0f && Math.Sign(direction.X) == Math.Sign(beforeDashSpeed.X) && Math.Abs(beforeDashSpeed.X) > dashSpeed)
            dashSpeed = Math.Abs(beforeDashSpeed.X);

        player.DoWhiteDash(direction, dashSpeed);
    }

    private static void WhiteEnd(Player player) {
        var rushData = player.GetData();

        player.Sprite.Scale = Vector2.One;
        player.Sprite.Rotation = 0f;
        player.Sprite.Origin.Y = 32f;
        player.Sprite.Position.Y = 0f;
        player.Hair.Visible = true;
        player.Stop(rushData.WhiteSoundSource);
        player.Play(SFX.char_bad_dreamblock_exit);
        player.Play(SFX.game_05_redbooster_end);
    }

    private static float GetRedBounceSpeed(this Player player) {
        float beforeSpeedX = Math.Abs(player.Speed.X);

        if (player.wallSpeedRetentionTimer > 0f)
            beforeSpeedX = Math.Max(beforeSpeedX, Math.Abs(player.wallSpeedRetained));

        return Math.Max(130f, beforeSpeedX + RED_BOUNCE_ADD_SPEED);
    }

    private static float GetWallBoostSpeed(float value, Player player)
        => player.TryGetData(out var rushData) && rushData.RedBoostTimer > 0f ? player.GetRedBounceSpeed() : value;

    private static float GetGroundJumpGraceTime(float value, Player player)
        => player.TryGetData(out var rushData) && player.StateMachine.State == rushData.StBlue ? BLUE_HYPER_GRACE_TIME_GROUND : value;

    private static bool IsInTransitionableState(Player player) {
        if (!player.TryGetData(out var rushData))
            return false;

        int state = player.StateMachine.State;

        return state == rushData.StGreen || state == rushData.StWhite;
    }

    private static bool ShouldHitPlatform(Player player, CollisionData data)
        => player.TryGetData(out var rushData) && rushData.RedBoostTimer > 0f && data.Hit?.OnDashCollide != null
           && (data.Direction.X == Math.Sign(player.Speed.X) || data.Direction.Y == Math.Sign(player.Speed.Y));

    private static float GetWallSpeedRetentionTime(float value, Player player)
        => player.TryGetData(out var rushData) && rushData.RedBoostTimer > 0f ? RED_WALL_SPEED_RETENTION_TIME : value;

    private static void OnTrueCollideH(Player player) {
        if (player.TryGetData(out var rushData) && player.StateMachine.State == rushData.StWhite)
            player.StateMachine.State = Player.StHitSquash;
    }

    private static void OnTrueCollideV(Player player) {
        if (!player.TryGetData(out var rushData))
            return;

        int state = player.StateMachine.State;

        if (state == rushData.StGreen)
            player.DoGreenSlam();
        else if (state == rushData.StWhite)
            player.StateMachine.State = Player.StHitSquash;
    }

    private static bool ShouldGroundAccel(this Player player)
        => player.TryGetData(out var rushData) && rushData.RedBoostTimer > 0f && player.onGround;

    private static float GetMaxRun(float value, Player player) => player.ShouldGroundAccel() ? RED_ACCEL_SPEED : value;

    private static float GetRunAccel(float value, Player player) => player.ShouldGroundAccel() ? RED_ACCEL_ACCELERATION : value;

    private static float GetFriction(float value, Player player)
        => player.TryGetData(out var rushData) && rushData.RedBoostTimer > 0f ? 0f : value;

    private static void InsertUseCard(ILContext il) => InsertUseCard(new ILCursor(il));

    private static void InsertUseCard(ILCursor cursor) {
        cursor.GotoNext(MoveType.AfterLabel,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchCallvirt<Player>("get_CanDash"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(CheckUseCard);

        var label = cursor.DefineLabel();

        cursor.Emit(OpCodes.Brfalse_S, label);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(UseCard);
        cursor.Emit(OpCodes.Ret);
        cursor.MarkLabel(label);
    }

    private static void InsertResetRedBoostTimer(Action<Player> stateBegin, Player player) {
        if (player.TryGetData(out var rushData))
            rushData.RedBoostTimer = 0f;

        stateBegin(player);
    }

    private static bool Player_get_DashAttacking(Func<Player, bool> dashAttacking, Player player) => dashAttacking(player) || player.IsInCustomDash();

    private static void Player_orig_Update_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(130f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetWallBoostSpeed);

        cursor.GotoNext(instr => instr.MatchStfld<Player>("jumpGraceTimer"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetGroundJumpGraceTime);
    }

    private static void Player_Update(On.Celeste.Player.orig_Update update, Player player) {
        if (player.TryGetData(out var rushData)) {
            rushData.RedBoostTimer -= Engine.DeltaTime;

            if (rushData.RedBoostTimer < 0f)
                rushData.RedBoostTimer = 0f;

            rushData.RedLateBounceTimer -= Engine.DeltaTime;

            if (rushData.RedLateBounceTimer < 0f)
                rushData.RedLateBounceTimer = 0f;
        }

        update(player);

        if (rushData == null && !player.TryGetData(out rushData))
            return;

        var redParticleEmitter = rushData.RedParticleEmitter;

        if (rushData.RedBoostTimer > 0f && player.Speed.Length() > 64f) {
            redParticleEmitter.Interval = 0.008f / Math.Min(player.Speed.Length() / RED_DASH_SPEED, 1f);
            redParticleEmitter.Start();
        }
        else
            redParticleEmitter.Stop();

        if (rushData.RedBoostTimer > 0f)
            player.UpdateTrail(Color.Red * Math.Min(4f * rushData.RedBoostTimer, 1f), 0.16f);
        else
            player.Stop(rushData.RedSoundSource);
    }

    private static void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite updateSprite, Player player) {
        if (!player.IsInCustomDash()) {
            updateSprite(player);

            return;
        }

        var rushData = player.GetData();
        int state = player.StateMachine.State;
        var sprite = player.Sprite;

        if (state == rushData.StBlue)
            sprite.Play("dash");
        else if (state == rushData.StGreen)
            sprite.Play("fallFast");
        else if (state == rushData.StRed) {
            if (player.Ducking)
                sprite.Play("duck");
            else
                sprite.Play("dash");
        }
        else if (state == rushData.StWhite) {
            if (sprite.CurrentAnimationID != "dreamDashIn" && sprite.CurrentAnimationID != "dreamDashLoop")
                sprite.Play("dreamDashIn");

            var dashDir = player.DashDir;

            if (dashDir.X == 0f) {
                player.Sprite.Scale = new Vector2(0.67f, 1.5f);
                player.Sprite.Rotation = 0f;
            }
            else {
                player.Sprite.Scale = new Vector2(1.5f, 0.67f);
                player.Sprite.Rotation = (Math.Sign(dashDir.X) * dashDir).Angle();
            }

            player.Sprite.Origin.Y = 26f;
            player.Sprite.Position.Y = -6f;
        }
        else
            updateSprite(player);
    }

    private static void Player_BeforeDownTransition_il(ILContext il) {
        var cursor = new ILCursor(il);
        ILLabel label = null;

        cursor.GotoNext(MoveType.Before,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchCallvirt<StateMachine>("get_State"));
        cursor.FindNext(out _,
            instr => instr.OpCode == OpCodes.Ldc_I4_5,
            instr => instr.MatchBeq(out label));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(IsInTransitionableState);
        cursor.Emit(OpCodes.Brtrue_S, label);
    }

    private static void Player_BeforeUpTransition_il(ILContext il) {
        var cursor = new ILCursor(il);
        ILLabel label = null;

        while (cursor.TryGotoNext(MoveType.Before,
                   instr => instr.OpCode == OpCodes.Ldarg_0,
                   instr => instr.MatchLdfld<Player>("StateMachine"),
                   instr => instr.MatchCallvirt<StateMachine>("get_State"),
                   instr => instr.OpCode == OpCodes.Ldc_I4_5)) {
            cursor.FindNext(out _, instr => instr.MatchBeq(out label));

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitCall(IsInTransitionableState);
            cursor.Emit(OpCodes.Brtrue_S, label);

            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldc_I4_5);
        }
    }

    private static void Player_Jump(On.Celeste.Player.orig_Jump jump, Player player, bool particles, bool playsfx) {
        if (!player.TryGetData(out var rushData)) {
            jump(player, particles, playsfx);

            return;
        }

        jump(player, particles, playsfx);

        if (!player.dreamJump)
            return;

        if (player.moveX == Math.Sign(rushData.WhiteJumpSpeedReturn))
            player.Speed.X += rushData.WhiteJumpSpeedReturn;

        rushData.WhiteJumpSpeedReturn = 0f;
    }

    private static void Player_WallJump(On.Celeste.Player.orig_WallJump wallJump, Player player, int dir) {
        if (!player.TryGetData(out var rushData) || rushData.RedBoostTimer == 0f) {
            wallJump(player, dir);

            return;
        }

        float bounceSpeed = dir * player.GetRedBounceSpeed();

        wallJump(player, dir);
        bounceSpeed += player.LiftBoost.X;

        if (player.moveX == dir)
            player.Speed.X = bounceSpeed;
        else {
            rushData.RedLateBounceTimer = RED_LATE_BOUNCE_TIME;
            rushData.RedLateBounceSpeed = bounceSpeed;
        }
    }

    private static void Player_Rebound(On.Celeste.Player.orig_Rebound rebound, Player player, int direction) {
        if (!player.TryGetData(out var rushData)) {
            rebound(player, direction);

            return;
        }

        if (player.StateMachine.State == rushData.StGreen && direction == 0)
            player.DoGreenSlam();
        else if (rushData.RedBoostTimer > 0f)
            rushData.RedBoostTimer = 0f;

        rebound(player, direction);
    }

    private static PlayerDeadBody Player_Die(On.Celeste.Player.orig_Die die, Player player, Vector2 direction, bool evenifinvincible, bool registerdeathinstats)
        => !evenifinvincible && player.CollideFirst<RushGoal>()?.Warping is true ? null : die(player, direction, evenifinvincible, registerdeathinstats);

    private static void Player_OnCollideH_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.AfterLabel,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchCallvirt<Player>("get_DashAttacking"));

        var label = cursor.DefineLabel();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldarg_1);
        cursor.EmitCall(ShouldHitPlatform);
        cursor.Emit(OpCodes.Brtrue_S, label);

        cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Bne_Un);

        cursor.MarkLabel(label);

        cursor.GotoNext(MoveType.After,
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_2,
            instr => instr.MatchBeq(out label));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(IsInCustomDash);
        cursor.Emit(OpCodes.Brtrue_S, label);

        cursor.GotoNext(MoveType.Before, instr => instr.MatchStfld<Player>("wallSpeedRetentionTimer"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetWallSpeedRetentionTime);

        cursor.Index = -1;
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(OnTrueCollideH);
    }

    private static void Player_OnCollideV_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.AfterLabel,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchCallvirt<Player>("get_DashAttacking"));

        var label = cursor.DefineLabel();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldarg_1);
        cursor.EmitCall(ShouldHitPlatform);
        cursor.Emit(OpCodes.Brtrue_S, label);

        cursor.GotoNext(MoveType.After, instr => instr.OpCode == OpCodes.Bne_Un_S);

        cursor.MarkLabel(label);

        cursor.GotoNext(MoveType.After,
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_2,
            instr => instr.MatchBeq(out label));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(IsInCustomDash);
        cursor.Emit(OpCodes.Brtrue_S, label);

        cursor.Index = -1;
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(OnTrueCollideV);
    }

    private static void Player_OnBoundsH(On.Celeste.Player.orig_OnBoundsH onBoundsH, Player player) {
        onBoundsH(player);

        if (player.TryGetData(out var rushData) && player.StateMachine.State == rushData.StWhite)
            player.StateMachine.State = Player.StNormal;
    }

    private static void Player_OnBoundsV(On.Celeste.Player.orig_OnBoundsV onBoundsV, Player player) {
        onBoundsV(player);

        if (player.TryGetData(out var rushData) && player.StateMachine.State == rushData.StWhite)
            player.Die(Vector2.Zero);
    }

    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        int nextState = normalUpdate(player);

        if (nextState != Player.StNormal || !player.TryGetData(out var rushData))
            return nextState;

        if (rushData.RedLateBounceTimer == 0f || Input.MoveX.Value != Math.Sign(rushData.RedLateBounceSpeed))
            return Player.StNormal;

        rushData.RedLateBounceTimer = 0f;
        player.Speed.X = rushData.RedLateBounceSpeed;

        return Player.StNormal;
    }

    private static void Player_NormalUpdate_il(ILContext il) {
        var cursor = new ILCursor(il);

        InsertUseCard(cursor);

        cursor.Index = -1;
        cursor.GotoPrev(MoveType.After, instr => instr.MatchStloc(6));
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldloc_S, (byte) 6);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetMaxRun);
        cursor.Emit(OpCodes.Stloc_S, (byte) 6);

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(400f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetFriction);

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(1000f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetRunAccel);
    }

    private static void Player_DreamDashBegin(On.Celeste.Player.orig_DreamDashBegin dreamDashBegin, Player player) {
        if (player.TryGetData(out var rushData)) {
            rushData.WhiteJumpSpeedReturn = 0f;
            rushData.RedBoostTimer = 0f;
        }

        dreamDashBegin(player);
    }

    private class RushData : Component {
        public readonly int StYellow;
        public readonly int StBlue;
        public readonly int StGreen;
        public readonly int StRed;
        public readonly int StWhite;
        public readonly Queue<AbilityCardType> Cards;
        public readonly CardInventoryIndicator CardInventoryIndicator;
        public readonly Circle GreenKillCircle;
        public readonly SmoothParticleEmitter RedParticleEmitter;
        public readonly SoundSource RedSoundSource;
        public readonly SoundSource WhiteSoundSource;

        public bool JustUsedCard;
        public bool BlueHyperTimePassed;
        public float RedBoostTimer;
        public float RedLateBounceTimer;
        public float RedLateBounceSpeed;
        public bool WhiteRedirect;
        public float WhiteJumpSpeedReturn;

        public RushData(int stYellow, int stBlue, int stGreen, int stRed, int stWhite) : base(false, false) {
            StYellow = stYellow;
            StBlue = stBlue;
            StGreen = stGreen;
            StRed = stRed;
            StWhite = stWhite;
            Cards = new Queue<AbilityCardType>();
            CardInventoryIndicator = new CardInventoryIndicator();
            GreenKillCircle = new Circle(GREEN_LAND_KILL_RADIUS);
            RedParticleEmitter = new SmoothParticleEmitter(RED_PARTICLE, -8f * Vector2.UnitY, 6f * Vector2.One, 0f);
            RedSoundSource = new SoundSource();
            WhiteSoundSource = new SoundSource();
        }
    }
}