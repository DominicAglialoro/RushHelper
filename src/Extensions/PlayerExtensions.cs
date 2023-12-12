using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.RushHelper;

public static class PlayerExtensions {
    private const int MAX_CARD_COUNT = 3;
    private const float YELLOW_MIN_X = 90f;
    private const float YELLOW_ADD_X = 40f;
    private const float YELLOW_MIN_Y = -220f;
    private const float YELLOW_VAR_JUMP_TIME = 0.15f;
    private const float BLUE_SPEED = 720f;
    private const float BLUE_END_SPEED = 240f;
    private const float BLUE_DURATION = 0.15f;
    private const float BLUE_ALLOW_JUMP_AT = 0.05f;
    private const float BLUE_HYPER_GRACE_TIME = 0.05f;
    private const float GREEN_FALL_SPEED = 360f;
    private const float GREEN_LAND_SPEED = 90f;
    private const float GREEN_LAND_KILL_RADIUS = 48f;
    private static readonly Vector2 RED_DASH_SPEED = new(280f, 240f);
    private const float RED_DASH_DURATION = 0.15f;
    private const float RED_BOOST_DURATION = 0.66f;
    private const float RED_FRICTION_MULTIPLIER = 0.5f;
    private const float RED_WALL_JUMP_ADD_SPEED = 40f;
    private const float WHITE_SPEED = 280f;
    private const float WHITE_REDIRECT_ADD_SPEED = 40f;
    private const float WHITE_JUMP_GRACE_TIME = 0.05f;
    private const float SURF_SPEED = 280f;
    private const float SURF_ACCELERATION = 650f;
    private const float SURF_CROUCH_FRICTION_MULTIPLIER = 0.5f;

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

    private static readonly ParticleType SURF_PARTICLE = new() {
        Color = Color.Aquamarine,
        ColorMode = ParticleType.ColorModes.Static,
        FadeMode = ParticleType.FadeModes.Late,
        LifeMin = 0.016f,
        LifeMax = 0.033f,
        Size = 1f,
        SpeedMin = 200f,
        SpeedMax = 250f,
        DirectionRange = 0.7f
    };
    
    public static void Load() {
        On.Celeste.Player.Update += Player_Update;
        On.Celeste.Player.OnCollideH += Player_OnCollideH;
        IL.Celeste.Player.OnCollideH += Player_OnCollideH_il;
        On.Celeste.Player.OnCollideV += Player_OnCollideV;
        IL.Celeste.Player.OnCollideV += Player_OnCollideV_il;
        IL.Celeste.Player.BeforeDownTransition += Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition += Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.WallJump += Player_WallJump;
        On.Celeste.Player.NormalUpdate += Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate += Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin += Player_DashBegin;
        On.Celeste.Player.UpdateSprite += Player_UpdateSprite;
    }

    public static void Unload() {
        On.Celeste.Player.Update -= Player_Update;
        IL.Celeste.Player.OnCollideH -= Player_OnCollideH_il;
        IL.Celeste.Player.OnCollideV -= Player_OnCollideV_il;
        IL.Celeste.Player.BeforeDownTransition -= Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition -= Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump -= Player_Jump;
        On.Celeste.Player.WallJump -= Player_WallJump;
        IL.Celeste.Player.NormalUpdate -= Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin -= Player_DashBegin;
        On.Celeste.Player.UpdateSprite -= Player_UpdateSprite;
    }

    public static void RefillDashes(this Player player, int dashes) {
        if (dashes > player.Dashes)
            player.Dashes = dashes;
    }

    public static void ClearRushData(this Player player) {
        if (!player.TryGetData(out _, out var extData))
            return;
        
        var cards = extData.Cards;

        cards.Clear();
        extData.CardInventoryIndicator.UpdateInventory(cards);
        extData.BlueHyperTimePassed = false;
        extData.RedBoostTimer = 0f;
        extData.RedParticleEmitter.Active = false;
        extData.RedSoundSource.Stop();
        extData.WhiteRedirect = false;
        extData.WhiteSoundSource.Stop();
        extData.CustomTrailTimer = 0f;
        extData.Surfing = false;
        extData.SurfParticleEmitter.Active = false;
        extData.SurfSoundSource.Stop();
    }

    public static bool TryGiveCard(this Player player, AbilityCardType cardType) {
        player.GetOrCreateData(out _, out var extData);

        var cards = extData.Cards;

        if (cards.Count == MAX_CARD_COUNT)
            return false;
        
        cards.Enqueue(cardType);
        extData.CardInventoryIndicator.UpdateInventory(cards);
        extData.CardInventoryIndicator.PlayAnimation();

        return true;
    }

    public static bool HitDemon(this Player player) {
        if (!player.TryGetData(out var dynamicData, out var extData))
            return player.DashAttacking || player.StateMachine.State == 2;

        int state = player.StateMachine.State;
        
        if (!player.DashAttacking
            && extData.RedBoostTimer == 0f
            && state != 2
            && state != extData.BlueIndex
            && state != extData.GreenIndex
            && state != extData.WhiteIndex)
            return false;

        if (state == extData.BlueIndex)
            dynamicData.Set("jumpGraceTimer", BLUE_HYPER_GRACE_TIME);
        else if (state == extData.WhiteIndex && player.DashDir.X != 0f) {
            dynamicData.Set("jumpGraceTimer", WHITE_JUMP_GRACE_TIME);
            dynamicData.Set("dreamJump", true);
            player.StateMachine.State = 0;
        }

        return true;
    }

    private static void GetData(this Player player, out DynamicData dynamicData, out Data extData) {
        dynamicData = DynamicData.For(player);
        extData = dynamicData.Get<Data>("rushHelperData");
    }

    private static void GetOrCreateData(this Player player, out DynamicData dynamicData, out Data extData) {
        dynamicData = DynamicData.For(player);
        
        if (dynamicData.TryGet("rushHelperData", out extData))
            return;

        extData = new Data();
        dynamicData.Set("rushHelperData", extData);
        
        player.Add(extData.CardInventoryIndicator = new CardInventoryIndicator());
        
        var level = (Level) player.Scene;
        
        player.Add(extData.RedParticleEmitter = new SmoothParticleEmitter(level.ParticlesFG, RED_PARTICLE, Vector2.Zero, 6f * Vector2.One, 0f));
        extData.RedParticleEmitter.Active = false;
        
        player.Add(extData.SurfParticleEmitter = new SmoothParticleEmitter(level.ParticlesFG, SURF_PARTICLE, Vector2.Zero, 2f * Vector2.One, 0f));
        extData.SurfParticleEmitter.Active = false;
        
        player.Add(extData.RedSoundSource = new SoundSource());
        player.Add(extData.WhiteSoundSource = new SoundSource());
        player.Add(extData.SurfSoundSource = new SoundSource());

        var stateMachine = player.StateMachine;

        extData.YellowIndex = stateMachine.AddState(null, player.YellowCoroutine);
        extData.BlueIndex = stateMachine.AddState(player.BlueUpdate, player.BlueCoroutine, null, player.BlueEnd);
        extData.GreenIndex = stateMachine.AddState(player.GreenUpdate, player.GreenCoroutine);
        extData.RedIndex = stateMachine.AddState(player.RedUpdate, player.RedCoroutine);
        extData.WhiteIndex = stateMachine.AddState(player.WhiteUpdate, player.WhiteCoroutine, null, player.WhiteEnd);
    }

    private static bool TryGetData(this Player player, out DynamicData dynamicData, out Data extData) {
        dynamicData = DynamicData.For(player);

        return dynamicData.TryGet("rushHelperData", out extData);
    }

    private static bool CheckUseCard(this Player player) {
        if (!Input.Grab.Pressed
            || !player.TryGetData(out _, out var extData)
            || extData.Cards.Count == 0)
            return false;
        
        Input.Grab.ConsumeBuffer();

        return true;
    }

    private static bool NextCardIs(this Player player, AbilityCardType cardType) {
        if (!player.TryGetData(out _, out var extData))
            return false;

        var cards = extData.Cards;

        return cards.Count > 0 && cards.Peek() == cardType;
    }

    private static AbilityCardType PopCard(this Player player) {
        player.GetData(out _, out var extData);

        var cards = extData.Cards;
        var cardInventoryIndicator = extData.CardInventoryIndicator;
        var cardType = cards.Dequeue();
        
        cardInventoryIndicator.UpdateInventory(cards);
        cardInventoryIndicator.StopAnimation();

        return cardType;
    }
    
    private static int UseCard(this Player player) => player.PopCard() switch {
        AbilityCardType.Yellow => player.UseYellowCard(),
        AbilityCardType.Blue => player.UseBlueCard(),
        AbilityCardType.Green => player.UseGreenCard(),
        AbilityCardType.Red => player.UseRedCard(),
        AbilityCardType.White => player.UseWhiteCard(),
        _ => throw new ArgumentOutOfRangeException()
    };

    private static int UseYellowCard(this Player player) {
        player.GetData(out var dynamicData, out var extData);
        
        Audio.Play(SFX.game_gen_thing_booped, player.Position);
        Celeste.Freeze(0.016f);
        player.Scene.Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, MathHelper.PiOver2, Color.White));
        player.ResetStateValues();
        player.Sprite.Scale = new Vector2(0.4f, 1.8f);
        dynamicData.Set("beforeDashSpeed", player.Speed);
        player.Speed = Vector2.Zero;

        return extData.YellowIndex;
    }

    private static int UseBlueCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        extData.BlueHyperTimePassed = false;
        extData.CustomTrailTimer = 0.016f;
        player.Sprite.Play("dash");
        Audio.Play("event:/rushHelper/game/blue_dash", player.Position);
        Celeste.Freeze(0.05f);

        return extData.BlueIndex;
    }

    private static int UseGreenCard(this Player player) {
        player.GetData(out _, out var extData);
        player.PrepareForCustomDash();
        extData.CustomTrailTimer = 0.016f;
        player.Sprite.Play("fallFast");
        Audio.Play(SFX.game_05_crackedwall_vanish, player.Position);
        Celeste.Freeze(0.05f);

        return extData.GreenIndex;
    }

    private static int UseRedCard(this Player player) {
        player.GetData(out var dynamicData, out var extData);
        player.Speed += dynamicData.Get<Vector2>("LiftBoost");
        player.PrepareForCustomDash();
        player.Sprite.Play("dash");
        extData.CustomTrailTimer = 0.016f;
        Audio.Play("event:/rushHelper/game/red_boost_dash", player.Position);
        Celeste.Freeze(0.05f);

        return extData.RedIndex;
    }

    private static int UseWhiteCard(this Player player) {
        player.GetData(out var dynamicData, out var extData);
        player.Speed += dynamicData.Get<Vector2>("LiftBoost");
        player.PrepareForCustomDash();
        player.Sprite.Scale = Vector2.One;
        player.Sprite.Play("dreamDashLoop");
        player.Hair.Visible = false;
        Audio.Play(SFX.char_bad_dash_red_right, player.Position);
        extData.WhiteSoundSource.Play(SFX.char_mad_dreamblock_travel);
        extData.WhiteSoundSource.DisposeOnTransition = false;
        extData.CustomTrailTimer = 0.016f;
        Celeste.Freeze(0.05f);

        return extData.WhiteIndex;
    }

    private static void ResetStateValues(this Player player) {
        player.GetData(out var dynamicData, out _);
        
        player.AutoJump = false;
        player.AutoJumpTimer = 0f;
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("dashTrailTimer", 0f);
        dynamicData.Set("dashTrailCounter", 0);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("launched", false);
        dynamicData.Set("launchedTimer", 0f);
        dynamicData.Set("varJumpSpeed", 0f);
        dynamicData.Set("varJumpTimer", 0f);
        dynamicData.Set("wallBoostDir", 0);
        dynamicData.Set("wallBoostTimer", 0f);
    }

    private static void PrepareForCustomDash(this Player player) {
        player.GetData(out var dynamicData, out var extData);
        
        bool onGround = dynamicData.Get<bool>("onGround");

        player.ResetStateValues();
        dynamicData.Set("beforeDashSpeed", player.Speed);
        dynamicData.Set("dashStartedOnGround", onGround);
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;
        extData.RedBoostTimer = 0f;

        if (!onGround && player.Ducking && player.CanUnDuck)
            player.Ducking = false;
    }

    private static void DoWhiteDash(this Player player, Vector2 direction, float speed) {
        ((Level) player.Scene).Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        player.DashDir = direction;
        player.Speed = speed * direction;

        if (direction.X == 0f) {
            player.Sprite.Scale = new Vector2(0.5f, 2f);
            player.Sprite.Rotation = 0f;
        }
        else {
            player.Sprite.Scale = new Vector2(2f, 0.5f);
            player.Sprite.Rotation = (Math.Sign(direction.X) * direction).Angle();
        }
        
        player.Sprite.Origin.Y = 27f;
        player.Sprite.Position.Y = -6f;
    }

    private static void UpdateTrail(this Player player, Color color, float interval, float duration) {
        player.GetData(out _, out var extData);
        extData.CustomTrailTimer -= Engine.DeltaTime;

        if (extData.CustomTrailTimer > 0f)
            return;
        
        extData.CustomTrailTimer = interval;
        TrailManager.Add(player.Position, player.Sprite, player.Hair.Visible ? player.Hair : null,
            new Vector2((float) player.Facing * Math.Abs(player.Sprite.Scale.X), player.Sprite.Scale.Y),
            color, player.Depth + 1, duration);
    }

    private static bool IsInCustomDash(this Player player) {
        if (!player.TryGetData(out _, out var extData))
            return false;

        int state = player.StateMachine.State;

        return state == extData.BlueIndex
               || state == extData.GreenIndex
               || state == extData.RedIndex
               || state == extData.WhiteIndex;
    }

    private static IEnumerator YellowCoroutine(this Player player) {
        yield return null;
        
        player.GetData(out var dynamicData, out _);
        player.Speed = dynamicData.Get<Vector2>("beforeDashSpeed");
            
        int moveX = Input.MoveX.Value;
        var liftBoost = dynamicData.Get<Vector2>("LiftBoost");

        player.Speed.X += moveX * YELLOW_ADD_X + liftBoost.X;
            
        if (moveX != 0 && moveX * player.Speed.X < YELLOW_MIN_X)
            player.Speed.X = moveX * YELLOW_MIN_X;

        if (player.Speed.Y > YELLOW_MIN_Y)
            player.Speed.Y = YELLOW_MIN_Y;

        player.Speed.Y += liftBoost.Y;
        player.AutoJump = true;
        player.AutoJumpTimer = 0f;
        dynamicData.Set("varJumpSpeed", player.Speed.Y);
        dynamicData.Set("varJumpTimer", YELLOW_VAR_JUMP_TIME);
        player.StateMachine.State = 0;
    }

    private static int BlueUpdate(this Player player) {
        player.GetData(out var dynamicData, out var extData);
        player.UpdateTrail(Color.Blue, 0.016f, 0.66f);

        if (dynamicData.Get<float>("jumpGraceTimer") > BLUE_HYPER_GRACE_TIME || dynamicData.Get<bool>("onGround"))
            dynamicData.Set("jumpGraceTimer", BLUE_HYPER_GRACE_TIME);

        foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
            if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !dynamicData.Invoke<bool>("DashCorrectCheck", Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                player.MoveVExact((int) (jumpThru.Top - player.Bottom));
        }

        if (Input.Jump.Pressed
            && extData.BlueHyperTimePassed
            && dynamicData.Get<float>("jumpGraceTimer") > 0f) {
            player.Ducking = true;
            player.StateMachine.State = 0;
            dynamicData.Invoke("SuperJump");
            
            return 0;
        }

        return extData.BlueIndex;
    }

    private static IEnumerator BlueCoroutine(this Player player) {
        yield return null;

        player.GetData(out var dynamicData, out var extData);
        
        int aimX = Math.Sign(dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim")).X);

        if (aimX == 0)
            aimX = (int) player.Facing;

        player.DashDir.X = aimX;
        player.DashDir.Y = 0f;
        player.Speed.X = aimX * BLUE_SPEED;
        player.Speed.Y = 0f;
        player.Facing = (Facings) aimX;
        ((Level) player.Scene).Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        SlashFx.Burst(player.Center, player.DashDir.Angle());
        
        for (float timer = 0f; timer < BLUE_DURATION; timer += Engine.DeltaTime) {
            extData.BlueHyperTimePassed = timer >= BLUE_ALLOW_JUMP_AT;
            player.Sprite.Scale = Util.PreserveArea(Vector2.Lerp(new Vector2(2f, 0.5f), Vector2.One, timer / BLUE_DURATION));

            yield return null;
        }
        
        player.Speed.X = player.DashDir.X * BLUE_END_SPEED;
        player.StateMachine.State = 0;
    }

    private static void BlueEnd(this Player player) {
        player.GetData(out var dynamicData, out _);
        
        if (Math.Abs(player.Speed.X) > BLUE_END_SPEED)
            player.Speed.X = Math.Sign(player.Speed.X) * BLUE_END_SPEED;

        float wallSpeedRetained = dynamicData.Get<float>("wallSpeedRetained");
        
        if (Math.Sign(wallSpeedRetained) != Math.Sign(player.Speed.X)) {
            dynamicData.Set("wallSpeedRetained", 0f);
            dynamicData.Set("wallSpeedRetentionTimer", 0f);
        }
        else if (Math.Abs(wallSpeedRetained) > Math.Abs(player.Speed.X))
            dynamicData.Set("wallSpeedRetained", player.Speed.X);
        
        dynamicData.Set("jumpGraceTimer", 0f);
        
        player.Sprite.Scale = Vector2.One;
    }

    private static int GreenUpdate(this Player player) {
        player.GetData(out var dynamicData, out var extData);

        if (Input.Jump.Pressed) {
            if (dynamicData.Invoke<bool>("WallJumpCheck", 1)) {
                dynamicData.Invoke("WallJump", -1);

                return 0;
            }
            
            if (dynamicData.Invoke<bool>("WallJumpCheck", -1)) {
                dynamicData.Invoke("WallJump", 1);

                return 0;
            }
        }
        
        if (player.CanDash) {
            player.Sprite.Scale = Vector2.One;
            
            return player.StartDash();
        }

        if (!player.NextCardIs(AbilityCardType.Green) && player.CheckUseCard()) {
            player.Sprite.Scale = Vector2.One;

            return player.UseCard();
        }
        
        player.UpdateTrail(Color.Green, 0.016f, 0.33f);
        player.Sprite.Scale = new Vector2(0.5f, 2f);
        
        return extData.GreenIndex;
    }

    private static IEnumerator GreenCoroutine(this Player player) {
        yield return null;
        
        player.Speed = new Vector2(0f, GREEN_FALL_SPEED);
    }

    private static int RedUpdate(this Player player) {
        player.GetData(out var dynamicData, out var extData);

        var dashDir = dynamicData.Get<Vector2>("dashDir");
        
        if (Input.Jump.Pressed) {
            if (player.DashDir.Y >= 0f && dynamicData.Get<float>("jumpGraceTimer") > 0f) {
                player.Jump();

                return 0;
            }

            if (dynamicData.Invoke<bool>("WallJumpCheck", 1)) {
                dynamicData.Invoke("WallJump", -1);

                return 0;
            }
                
            if (dynamicData.Invoke<bool>("WallJumpCheck", -1)) {
                dynamicData.Invoke("WallJump", 1);

                return 0;
            }
        }

        if (player.DashDir.Y == 0f) {
            foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
                if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !dynamicData.Invoke<bool>("DashCorrectCheck", Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                    player.MoveVExact((int) (jumpThru.Top - player.Bottom));
            }

            return extData.RedIndex;
        }

        return extData.RedIndex;
    }

    private static IEnumerator RedCoroutine(this Player player) {
        yield return null;
        
        player.GetData(out var dynamicData, out var extData);
        ((Level) player.Scene).Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        extData.RedBoostTimer = RED_BOOST_DURATION;
        extData.RedSoundSource.Play("event:/rushHelper/game/red_boost_sustain");
        extData.RedSoundSource.DisposeOnTransition = false;

        var beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed");
        
        player.DashDir = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
        player.Speed = RED_DASH_SPEED * player.DashDir;
        
        if (Math.Sign(player.Speed.X) == Math.Sign(beforeDashSpeed.X) && Math.Abs(beforeDashSpeed.X) > Math.Abs(player.Speed.X))
            player.Speed.X = beforeDashSpeed.X;
        
        if (Math.Sign(player.Speed.Y) == Math.Sign(beforeDashSpeed.Y) && Math.Abs(beforeDashSpeed.Y) > Math.Abs(player.Speed.Y))
            player.Speed.Y = beforeDashSpeed.Y;

        if (dynamicData.Get<bool>("onGround") && player.DashDir.X != 0f && player.DashDir.Y > 0f) {
            player.DashDir.X = Math.Sign(player.DashDir.X);
            player.DashDir.Y = 0f;
            player.Ducking = true;
            player.Speed.X *= 1.2f;
            player.Speed.Y = 0f;
        }
        
        SlashFx.Burst(player.Center, player.DashDir.Angle());

        yield return RED_DASH_DURATION;

        player.StateMachine.State = 0;
    }

    private static int WhiteUpdate(this Player player) {
        player.GetData(out var dynamicData, out var extData);
        player.UpdateTrail(Color.White, 0.016f, 0.33f);

        if (extData.WhiteRedirect) {
            var direction = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
            float dashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").Length() + WHITE_REDIRECT_ADD_SPEED;

            if (dashSpeed < WHITE_SPEED)
                dashSpeed = WHITE_SPEED;
            
            player.DoWhiteDash(direction, dashSpeed);
            extData.WhiteRedirect = false;

            return extData.WhiteIndex;
        }
        
        if (player.CanDash)
            return player.StartDash();

        if (!player.CheckUseCard())
            return extData.WhiteIndex;

        if (!player.NextCardIs(AbilityCardType.White))
            return player.UseCard();

        player.PopCard();
        dynamicData.Set("beforeDashSpeed", player.Speed);
        player.DashDir = Vector2.Zero;
        player.Speed = Vector2.Zero;
        player.Sprite.Scale = Vector2.One;
        Audio.Play(SFX.char_bad_dash_red_right, player.Position);
        Celeste.Freeze(0.033f);
        extData.WhiteRedirect = true;

        return extData.WhiteIndex;
    }

    private static IEnumerator WhiteCoroutine(this Player player) {
        yield return null;
        
        player.GetData(out var dynamicData, out _);
        
        var direction = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
        float beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").X;
        float dashSpeed = WHITE_SPEED;

        if (direction.X != 0f && Math.Sign(direction.X) == Math.Sign(beforeDashSpeed) && Math.Abs(beforeDashSpeed) > dashSpeed)
            dashSpeed = Math.Abs(beforeDashSpeed);
        
        player.DoWhiteDash(direction, dashSpeed);
    }

    private static void WhiteEnd(this Player player) {
        player.GetData(out _, out var extData);
        player.Sprite.Scale = Vector2.One;
        player.Sprite.Rotation = 0f;
        player.Sprite.Origin.Y = 32f;
        player.Sprite.Position.Y = 0f;
        player.Hair.Visible = true;
        extData.WhiteSoundSource.Stop();
        Audio.Play(SFX.char_bad_dreamblock_exit);
        Audio.Play(SFX.game_05_redbooster_end);
    }

    private static void OnTrueCollideH(Player player) {
        if (!player.TryGetData(out _, out var extData))
            return;
        
        if (player.StateMachine.State == extData.WhiteIndex)
            player.StateMachine.State = 0;
    }

    private static void OnTrueCollideV(Player player) {
        if (!player.TryGetData(out _, out var extData))
            return;

        int state = player.StateMachine.State;

        if (state == extData.GreenIndex) {
            player.Speed.X = Math.Sign(Input.MoveX.Value) * GREEN_LAND_SPEED;
            player.Sprite.Scale = new Vector2(1.5f, 0.75f);
            Audio.Play(SFX.game_gen_fallblock_impact, player.Position);
            Celeste.Freeze(0.05f);
            player.StateMachine.State = 0;

            var level = (Level) player.Scene;

            level.Particles.Emit(Player.P_SummitLandA, 12, player.BottomCenter, Vector2.UnitX * 3f, -1.5707964f);
            level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter - Vector2.UnitX * 2f, Vector2.UnitX * 2f, 3.403392f);
            level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter + Vector2.UnitX * 2f, Vector2.UnitX * 2f, -0.2617994f);
            level.Displacement.AddBurst(player.Center, 0.4f, 16f, 128f, 1f, Ease.QuadOut, Ease.QuadOut);

            int dashRestores = Demon.KillInRadius(player.Scene, player.Center, GREEN_LAND_KILL_RADIUS);

            player.RefillDashes(dashRestores);

            if (dashRestores >= 2)
                Audio.Play(SFX.game_10_pinkdiamond_touch, player.Position);
        }
        else if (state == extData.WhiteIndex)
            player.StateMachine.State = 0;
    }

    private static bool IsInTransitionableState(Player player) {
        if (!player.TryGetData(out _, out var extData))
            return false;

        int state = player.StateMachine.State;

        return state == extData.GreenIndex || state == extData.WhiteIndex;
    }

    private static float MultiplyAirFriction(float value, Player player)
        => player.TryGetData(out _, out var extData) && extData.RedBoostTimer > 0f ? value * RED_FRICTION_MULTIPLIER : value;

    private static float MultiplyGroundFriction(float value, Player player) {
        if (!player.TryGetData(out var dynamicData, out var extData))
            return value;
        
        int moveX = dynamicData.Get<int>("moveX");
        bool movingWithDirection = moveX != 0 && moveX == Math.Sign(player.Speed.X);

        if (extData.Surfing) {
            if (player.Ducking)
                value *= SURF_CROUCH_FRICTION_MULTIPLIER;
            else if (movingWithDirection)
                return 0f;
        }

        if (extData.RedBoostTimer > 0f && (movingWithDirection || player.Ducking))
            value *= RED_FRICTION_MULTIPLIER;

        return value;
    }

    private static void Player_Update(On.Celeste.Player.orig_Update update, Player player) {
        player.GetData(out var dynamicData, out var extData);
        
        if (player.CollideCheck<SurfPlatform>(player.Position + Vector2.UnitY)) {
            if (extData == null)
                player.GetOrCreateData(out _, out extData);

            if (!extData.Surfing) {
                Audio.Play(SFX.char_mad_water_in);
                extData.SurfSoundSource.Play("event:/rushHelper/game/surf", "fade", MathHelper.Min(Math.Abs(player.Speed.X) / SURF_SPEED, 1f));
                extData.Surfing = true;
            }
        }
        else if (extData != null && extData.Surfing) {
            extData.SurfSoundSource.Stop();
            extData.Surfing = false;
        }
        
        if (extData == null) {
            update(player);
            
            return;
        }

        extData.RedBoostTimer -= Engine.DeltaTime;
        
        if (extData.RedBoostTimer < 0f)
            extData.RedBoostTimer = 0f;

        update(player);

        var redBoostParticleEmitter = extData.RedParticleEmitter;
        
        if (extData.RedBoostTimer > 0f && player.Speed.Length() > 64f) {
            redBoostParticleEmitter.Interval = 0.008f / Math.Min(Math.Abs(player.Speed.Length()) / RED_DASH_SPEED.X, 1f);
            redBoostParticleEmitter.Position = -8f * Vector2.UnitY;
            redBoostParticleEmitter.Start();
        }
        else if (redBoostParticleEmitter.Active)
            redBoostParticleEmitter.Stop();

        var surfParticleEmitter = extData.SurfParticleEmitter;

        if (extData.Surfing && dynamicData.Get<bool>("onGround") && Math.Abs(player.Speed.X) > 64f) {
            surfParticleEmitter.Interval = 0.008f / Math.Min(Math.Abs(player.Speed.X) / SURF_SPEED, 1f);
            surfParticleEmitter.Position = new Vector2(-4f * Math.Sign(player.Speed.X), -2f);
            surfParticleEmitter.Direction = -MathHelper.PiOver2 - 0.4f * Math.Sign(player.Speed.X);
            surfParticleEmitter.Start();
        }
        else if (surfParticleEmitter.Active)
            surfParticleEmitter.Stop();

        if (extData.RedBoostTimer > 0f)
            player.UpdateTrail(Color.Red * Math.Min(4f * extData.RedBoostTimer, 1f), 0.016f, 0.16f);
        else if (extData.RedSoundSource.Playing)
            extData.RedSoundSource.Stop();

        if (extData.SurfSoundSource.Playing)
            extData.SurfSoundSource.Param("fade", MathHelper.Min(Math.Abs(player.Speed.X) / SURF_SPEED, 1f));
    }

    private static void Player_OnCollideH(On.Celeste.Player.orig_OnCollideH onCollideH, Player player, CollisionData data) {
        if (data.Hit is DashBlock dashBlock && player.TryGetData(out _, out var extData) && (extData.RedBoostTimer > 0f || player.StateMachine.State == extData.BlueIndex))
            dashBlock.Break(player.Center, data.Direction, true, true);
        else
            onCollideH(player, data);
    }

    private static void Player_OnCollideH_il(ILContext il) {
        var cursor = new ILCursor(il);
        ILLabel label = null;

        cursor.GotoNext(MoveType.After,
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_2,
            instr => instr.MatchBeq(out label));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInCustomDash)));
        cursor.Emit(OpCodes.Brtrue_S, label);
        
        cursor.Index = -1;
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(OnTrueCollideH)));
    }

    private static void Player_OnCollideV(On.Celeste.Player.orig_OnCollideV onCollideV, Player player, CollisionData data) {
        if (data.Hit is DashBlock dashBlock && player.TryGetData(out _, out var extData) && (extData.RedBoostTimer > 0f || player.StateMachine.State == extData.GreenIndex))
            dashBlock.Break(player.Center, data.Direction, true, true);
        else
            onCollideV(player, data);
    }

    private static void Player_OnCollideV_il(ILContext il) {
        var cursor = new ILCursor(il);
        ILLabel label = null;
        
        cursor.GotoNext(MoveType.After,
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_2,
            instr => instr.MatchBeq(out label));
        
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInCustomDash)));
        cursor.Emit(OpCodes.Brtrue_S, label);

        cursor.Index = -1;
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(OnTrueCollideV)));
    }

    private static void Player_BeforeDownTransition_il(ILContext il) {
        var cursor = new ILCursor(il);
        ILLabel label = null;
        
        cursor.GotoNext(MoveType.Before,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchLdfld<Player>("StateMachine"),
            instr => instr.MatchCallvirt<StateMachine>("get_State"),
            instr => instr.OpCode == OpCodes.Ldc_I4_5);
        cursor.FindNext(out _, instr => instr.MatchBeq(out label));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInTransitionableState)));
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
            cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(IsInTransitionableState)));
            cursor.Emit(OpCodes.Brtrue_S, label);

            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldc_I4_5);
        }
    }

    private static void Player_Jump(On.Celeste.Player.orig_Jump jump, Player player, bool particles, bool playsfx) {
        if (player.TryGetData(out _, out var extData) && extData.Surfing)
            Util.PlaySound(SFX.char_mad_water_out, 2f);
            
        jump(player, particles, playsfx);
    }

    private static void Player_WallJump(On.Celeste.Player.orig_WallJump wallJump, Player player, int dir) {
        if (!player.TryGetData(out var dynamicData, out var extData) || extData.RedBoostTimer == 0f) {
            wallJump(player, dir);
            
            return;
        }
        
        float beforeSpeedX = Math.Abs(player.Speed.X);
        float beforeSpeedY = player.Speed.Y;
        
        if (dynamicData.Get<float>("wallSpeedRetentionTimer") > 0f)
            beforeSpeedX = Math.Max(beforeSpeedX, Math.Abs(dynamicData.Get<float>("wallSpeedRetained")));
        
        wallJump(player, dir);

        var liftBoost = dynamicData.Get<Vector2>("LiftBoost");

        if (Math.Sign(Input.MoveX.Value) == dir)
            player.Speed.X = dir * Math.Max(130f, beforeSpeedX + RED_WALL_JUMP_ADD_SPEED) + liftBoost.X;

        player.Speed.Y = Math.Min(-105f, beforeSpeedY) + liftBoost.Y;
        dynamicData.Set("varJumpSpeed", player.Speed.Y);
    }

    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        int nextState = normalUpdate(player);
        
        if (nextState != 0 || !player.TryGetData(out var dynamicData, out var extData))
            return nextState;
        
        int moveX = dynamicData.Get<int>("moveX");
        
        if (moveX != 0 && extData.Surfing && dynamicData.Get<bool>("onGround") && !player.Ducking && moveX * player.Speed.X < SURF_SPEED)
            player.Speed.X = Calc.Approach(player.Speed.X, moveX * SURF_SPEED, Engine.DeltaTime * SURF_ACCELERATION);

        return 0;
    }

    private static void Player_NormalUpdate_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.AfterLabel,
            instr => instr.OpCode == OpCodes.Ldarg_0,
            instr => instr.MatchCallvirt<Player>("get_CanDash"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(CheckUseCard)));
        
        var label = cursor.DefineLabel();
        
        cursor.Emit(OpCodes.Brfalse_S, label);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(UseCard)));
        cursor.Emit(OpCodes.Ret);
        cursor.MarkLabel(label);

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(500f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(MultiplyGroundFriction)));

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(0.65f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(MultiplyAirFriction)));

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(1f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Call, typeof(PlayerExtensions).GetMethodUnconstrained(nameof(MultiplyGroundFriction)));
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin dashBegin, Player player) {
        if (player.TryGetData(out _, out var extData))
            extData.RedBoostTimer = 0f;
        
        foreach (var entity in player.CollideAll<Demon>())
            ((Demon) entity).OnPlayer(player);

        dashBegin(player);
    }

    private static void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite updateSprite, Player player) {
        if (!player.IsInCustomDash()) {
            updateSprite(player);

            return;
        }
        
        player.GetData(out _, out var extData);

        int state = player.StateMachine.State;
        
        if (state == extData.BlueIndex)
            player.Sprite.Play("dash");
        else if (state == extData.RedIndex) {
            if (player.Ducking)
                player.Sprite.Play("duck");
            else
                player.Sprite.Play("dash");
        }
    }

    private class Data {
        public int YellowIndex;
        public int BlueIndex;
        public int GreenIndex;
        public int RedIndex;
        public int WhiteIndex;
        public Queue<AbilityCardType> Cards = new();
        public CardInventoryIndicator CardInventoryIndicator;
        public bool BlueHyperTimePassed;
        public float RedBoostTimer;
        public SmoothParticleEmitter RedParticleEmitter;
        public SoundSource RedSoundSource;
        public bool WhiteRedirect;
        public SoundSource WhiteSoundSource;
        public float CustomTrailTimer;
        public bool Surfing;
        public SmoothParticleEmitter SurfParticleEmitter;
        public SoundSource SurfSoundSource;
    }
}