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
    private const float YELLOW_Y = -220f;
    private const float YELLOW_VAR_JUMP_TIME = 0.15f;
    private const float BLUE_SPEED = 720f;
    private const float BLUE_END_SPEED = 240f;
    private const float BLUE_DURATION = 0.15f;
    private const float BLUE_ALLOW_JUMP_AT = 0.05f;
    private const float BLUE_HYPER_GRACE_TIME_GROUND = 0.05f;
    private const float BLUE_HYPER_GRACE_TIME_DEMON = 0.1f;
    private const float GREEN_FALL_SPEED = 360f;
    private const float GREEN_LAND_SPEED = 90f;
    private const float GREEN_LAND_KILL_RADIUS = 48f;
    private static readonly Vector2 RED_DASH_SPEED = new(280f, 240f);
    private const float RED_DASH_DURATION = 0.15f;
    private const float RED_BOOST_DURATION = 1f;
    private const float RED_LATE_BOUNCE_TIME = 0.1f;
    private const float RED_FRICTION_MULTIPLIER = 0.5f;
    private const float RED_WALL_JUMP_ADD_SPEED = 40f;
    private const float RED_WALL_SPEED_RETENTION_TIME = 0.1f;
    private const float WHITE_SPEED = 280f;
    private const float WHITE_ACCELERATE_MULT = 1.2f;
    private const float WHITE_JUMP_GRACE_TIME = 0.1f;
    private const float SURF_SPEED = 280f;
    private const float SURF_ACCELERATION = 650f;

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

    private static ILHook il_Celeste_Player_orig_Update;

    public static void Load() {
        il_Celeste_Player_orig_Update = typeof(Player).CreateHook(nameof(Player.orig_Update), Player_orig_Update_il);
        On.Celeste.Player.Update += Player_Update;
        On.Celeste.Player.Die += Player_Die;
        On.Celeste.Player.OnCollideH += Player_OnCollideH;
        IL.Celeste.Player.OnCollideH += Player_OnCollideH_il;
        On.Celeste.Player.OnCollideV += Player_OnCollideV;
        IL.Celeste.Player.OnCollideV += Player_OnCollideV_il;
        On.Celeste.Player.OnBoundsH += Player_OnBoundsH;
        On.Celeste.Player.OnBoundsV += Player_OnBoundsV;
        IL.Celeste.Player.BeforeDownTransition += Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition += Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump += Player_Jump;
        On.Celeste.Player.WallJump += Player_WallJump;
        IL.Celeste.Player.WallJumpCheck += Player_WallJumpCheck_il;
        On.Celeste.Player.NormalUpdate += Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate += Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin += Player_DashBegin;
        On.Celeste.Player.UpdateSprite += Player_UpdateSprite;
    }

    public static void Unload() {
        il_Celeste_Player_orig_Update.Dispose();
        On.Celeste.Player.Update -= Player_Update;
        On.Celeste.Player.Die -= Player_Die;
        On.Celeste.Player.OnCollideH -= Player_OnCollideH;
        IL.Celeste.Player.OnCollideH -= Player_OnCollideH_il;
        On.Celeste.Player.OnCollideV -= Player_OnCollideV;
        IL.Celeste.Player.OnCollideV -= Player_OnCollideV_il;
        On.Celeste.Player.OnBoundsH -= Player_OnBoundsH;
        On.Celeste.Player.OnBoundsV -= Player_OnBoundsV;
        IL.Celeste.Player.BeforeDownTransition -= Player_BeforeDownTransition_il;
        IL.Celeste.Player.BeforeUpTransition -= Player_BeforeUpTransition_il;
        On.Celeste.Player.Jump -= Player_Jump;
        On.Celeste.Player.WallJump -= Player_WallJump;
        IL.Celeste.Player.WallJumpCheck -= Player_WallJumpCheck_il;
        On.Celeste.Player.NormalUpdate -= Player_NormalUpdate;
        IL.Celeste.Player.NormalUpdate -= Player_NormalUpdate_il;
        On.Celeste.Player.DashBegin -= Player_DashBegin;
        On.Celeste.Player.UpdateSprite -= Player_UpdateSprite;
    }

    public static void ResetStateValues(this Player player) {
        player.GetData(out var dynamicData, out var rushData);

        player.StateMachine.State = 0;
        player.Speed = Vector2.Zero;
        player.Dashes = 1;
        player.Sprite.Scale = Vector2.One;
        player.AutoJump = false;
        player.AutoJumpTimer = 0f;
        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("dashTrailTimer", 0f);
        dynamicData.Set("dashTrailCounter", 0);
        dynamicData.Set("forceMoveXTimer", 0f);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("launched", false);
        dynamicData.Set("launchedTimer", 0f);
        dynamicData.Set("varJumpSpeed", 0f);
        dynamicData.Set("varJumpTimer", 0f);
        dynamicData.Set("wallBoostDir", 0);
        dynamicData.Set("wallBoostTimer", 0f);

        if (rushData == null)
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
        rushData.RedSoundSource.Stop();
        rushData.WhiteRedirect = false;
        rushData.WhiteJumpSpeedReturn = 0f;
        rushData.WhiteSoundSource.Stop();
        rushData.Surfing = false;
        rushData.SurfSoundSource.Stop();
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
        player.GetOrCreateData(out _, out var rushData);

        var cards = rushData.Cards;

        if (cards.Count == MAX_CARD_COUNT)
            return false;

        cards.Enqueue(cardType);
        rushData.CardInventoryIndicator.UpdateInventory(cards);
        rushData.CardInventoryIndicator.PlayAnimation();

        return true;
    }

    public static bool HitDemon(this Player player) {
        if (!player.TryGetData(out var dynamicData, out var rushData))
            return player.DashAttacking || player.StateMachine.State == 2;

        int state = player.StateMachine.State;

        if (!player.DashAttacking
            && rushData.RedBoostTimer == 0f
            && state != 2
            && state != rushData.BlueIndex
            && state != rushData.GreenIndex
            && state != rushData.WhiteIndex)
            return false;

        if (state == rushData.BlueIndex) {
            if (!rushData.JustUsedCard)
                dynamicData.Set("jumpGraceTimer", BLUE_HYPER_GRACE_TIME_DEMON);
        }
        else if (state == rushData.WhiteIndex) {
            if (rushData.JustUsedCard)
                return false;

            if (player.DashDir.X != 0f) {
                dynamicData.Set("dreamJump", true);
                rushData.WhiteJumpSpeedReturn = Math.Sign(player.Speed.X) * (player.Speed.Length() - Math.Abs(player.Speed.X));

                if (Input.Jump.Pressed)
                    player.Jump();

                dynamicData.Set("jumpGraceTimer", WHITE_JUMP_GRACE_TIME);
            }

            player.StateMachine.State = 0;
            Celeste.Freeze(0.05f);
        }

        return true;
    }

    public static bool IsInCustomDash(this Player player) {
        if (!player.TryGetData(out _, out var rushData))
            return false;

        int state = player.StateMachine.State;

        return state == rushData.BlueIndex
               || state == rushData.GreenIndex
               || state == rushData.RedIndex
               || state == rushData.WhiteIndex;
    }

    public static bool IsInDestroyBlockState(this Player player) {
        if (!player.TryGetData(out _, out var rushData))
            return false;

        if (rushData.RedBoostTimer > 0f)
            return true;

        int state = player.StateMachine.State;

        return state == rushData.BlueIndex
               || state == rushData.GreenIndex
               || state == rushData.RedIndex;
    }

    private static void GetData(this Player player, out DynamicData dynamicData, out RushData rushData) {
        dynamicData = DynamicData.For(player);
        rushData = dynamicData.Get<RushData>("rushHelperData");
    }

    private static void GetOrCreateData(this Player player, out DynamicData dynamicData, out RushData rushData) {
        dynamicData = DynamicData.For(player);

        if (dynamicData.TryGet("rushHelperData", out rushData))
            return;

        rushData = new RushData();
        dynamicData.Set("rushHelperData", rushData);

        player.Add(rushData.CardInventoryIndicator = new CardInventoryIndicator());

        var level = player.SceneAs<Level>();

        player.Add(rushData.RedParticleEmitter = new SmoothParticleEmitter(level.ParticlesFG, RED_PARTICLE, Vector2.Zero, 6f * Vector2.One, 0f));
        rushData.RedParticleEmitter.Active = false;

        player.Add(rushData.RedSoundSource = new SoundSource());
        player.Add(rushData.WhiteSoundSource = new SoundSource());
        player.Add(rushData.SurfSoundSource = new SoundSource());

        var stateMachine = player.StateMachine;

        rushData.YellowIndex = stateMachine.AddState(null, player.YellowCoroutine);
        rushData.BlueIndex = stateMachine.AddState(player.BlueUpdate, player.BlueCoroutine, null, player.BlueEnd);
        rushData.GreenIndex = stateMachine.AddState(player.GreenUpdate, player.GreenCoroutine);
        rushData.RedIndex = stateMachine.AddState(player.RedUpdate, player.RedCoroutine);
        rushData.WhiteIndex = stateMachine.AddState(player.WhiteUpdate, player.WhiteCoroutine, null, player.WhiteEnd);
    }

    private static bool TryGetData(this Player player, out DynamicData dynamicData, out RushData rushData) {
        dynamicData = DynamicData.For(player);

        return dynamicData.TryGet("rushHelperData", out rushData);
    }

    private static bool CheckUseCard(this Player player) {
        if (!Input.Grab.Pressed
            || !player.TryGetData(out _, out var rushData)
            || rushData.Cards.Count == 0)
            return false;

        Input.Grab.ConsumeBuffer();

        return true;
    }

    private static bool NextCardIs(this Player player, AbilityCardType cardType) {
        if (!player.TryGetData(out _, out var rushData))
            return false;

        var cards = rushData.Cards;

        return cards.Count > 0 && cards.Peek() == cardType;
    }

    private static AbilityCardType PopCard(this Player player) {
        player.GetData(out _, out var rushData);

        var cards = rushData.Cards;
        var cardInventoryIndicator = rushData.CardInventoryIndicator;
        var cardType = cards.Dequeue();

        cardInventoryIndicator.UpdateInventory(cards);
        cardInventoryIndicator.StopAnimation();

        return cardType;
    }

    private static int UseCard(this Player player) {
        player.GetData(out _, out var rushData);
        rushData.JustUsedCard = true;

        return player.PopCard() switch {
            AbilityCardType.Yellow => player.UseYellowCard(),
            AbilityCardType.Blue => player.UseBlueCard(),
            AbilityCardType.Green => player.UseGreenCard(),
            AbilityCardType.Red => player.UseRedCard(),
            AbilityCardType.White => player.UseWhiteCard(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static int UseYellowCard(this Player player) {
        player.GetData(out var dynamicData, out var rushData);

        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("forceMoveXTimer", 0f);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("jumpGraceTimer", 0f);
        dynamicData.Set("launched", false);
        dynamicData.Set("varJumpTimer", 0f);
        dynamicData.Set("wallBoostTimer", 0f);
        dynamicData.Set("wallSlideTimer", 1.2f);

        dynamicData.Set("beforeDashSpeed", player.Speed);
        player.Speed = Vector2.Zero;
        player.Sprite.Scale = new Vector2(0.67f, 1.5f);
        player.Scene.Add(Engine.Pooler.Create<SpeedRing>().Init(player.Center, MathHelper.PiOver2, Color.White));
        Audio.Play(SFX.game_gen_thing_booped, player.Position);
        Celeste.Freeze(0.016f);

        return rushData.YellowIndex;
    }

    private static int UseBlueCard(this Player player) {
        player.GetData(out var dynamicData, out var rushData);
        player.PrepareForCustomDash();
        dynamicData.Set("jumpGraceTimer", 0f);
        rushData.BlueHyperTimePassed = false;

        player.Sprite.Play("dash");
        Audio.Play("event:/rushHelper/game/blue_dash", player.Position);

        return rushData.BlueIndex;
    }

    private static int UseGreenCard(this Player player) {
        player.GetData(out var dynamicData, out var rushData);
        player.PrepareForCustomDash();
        dynamicData.Set("varJumpTimer", 0f);

        player.Sprite.Play("fallFast");
        Audio.Play(SFX.game_05_crackedwall_vanish, player.Position);

        return rushData.GreenIndex;
    }

    private static int UseRedCard(this Player player) {
        player.GetData(out var dynamicData, out var rushData);
        player.Speed += dynamicData.Get<Vector2>("LiftBoost");
        player.PrepareForCustomDash();
        player.Sprite.Play("dash");
        Audio.Play("event:/rushHelper/game/red_boost_dash", player.Position);

        return rushData.RedIndex;
    }

    private static int UseWhiteCard(this Player player) {
        player.GetData(out var dynamicData, out var rushData);
        player.Speed += dynamicData.Get<Vector2>("LiftBoost");
        player.PrepareForCustomDash();
        dynamicData.Set("varJumpTimer", 0f);

        player.Sprite.Scale = Vector2.One;
        player.Sprite.Play("dreamDashIn");
        player.Hair.Visible = false;
        Audio.Play(SFX.char_bad_dash_red_right, player.Position);
        rushData.WhiteSoundSource.Play(SFX.char_mad_dreamblock_travel);
        rushData.WhiteSoundSource.DisposeOnTransition = false;

        return rushData.WhiteIndex;
    }

    private static void PrepareForCustomDash(this Player player) {
        player.GetData(out var dynamicData, out var rushData);

        dynamicData.Set("dashAttackTimer", 0f);
        dynamicData.Set("forceMoveXTimer", 0f);
        dynamicData.Set("gliderBoostTimer", 0f);
        dynamicData.Set("launched", false);
        dynamicData.Set("wallSlideTimer", 1.2f);

        dynamicData.Set("beforeDashSpeed", player.Speed);
        dynamicData.Set("dashStartedOnGround", dynamicData.Get<bool>("onGround"));
        player.Speed = Vector2.Zero;
        player.DashDir = Vector2.Zero;

        if (player.Ducking && player.CanUnDuck)
            player.Ducking = false;

        rushData.RedBoostTimer = 0f;
        dynamicData.Set("dashTrailTimer", 0.016f);
        Celeste.Freeze(0.05f);
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
        var dynamicData = DynamicData.For(player);

        float dashTrailTimer = dynamicData.Get<float>("dashTrailTimer") - Engine.DeltaTime;

        if (dashTrailTimer > 0f) {
            dynamicData.Set("dashTrailTimer", dashTrailTimer);

            return;
        }

        dynamicData.Set("dashTrailTimer", 0.016f);
        TrailManager.Add(player.Position, player.Sprite, player.Hair.Visible ? player.Hair : null,
            new Vector2((float) player.Facing * Math.Abs(player.Sprite.Scale.X), player.Sprite.Scale.Y),
            color, player.Depth + 1, duration);
    }

    private static IEnumerator YellowCoroutine(this Player player) {
        yield return null;

        player.GetData(out var dynamicData, out var rushData);
        rushData.JustUsedCard = false;
        player.Speed = dynamicData.Get<Vector2>("beforeDashSpeed");

        int moveX = Input.MoveX.Value;
        var liftBoost = dynamicData.Get<Vector2>("LiftBoost");

        player.Speed.X += moveX * YELLOW_ADD_X + liftBoost.X;

        if (moveX != 0 && moveX * player.Speed.X < YELLOW_MIN_X)
            player.Speed.X = moveX * YELLOW_MIN_X;

        player.Speed.Y = YELLOW_Y + liftBoost.Y;
        player.AutoJump = true;
        player.AutoJumpTimer = 0f;
        dynamicData.Set("varJumpSpeed", player.Speed.Y);
        dynamicData.Set("varJumpTimer", YELLOW_VAR_JUMP_TIME);
        player.StateMachine.State = 0;
    }

    private static int BlueUpdate(this Player player) {
        player.GetData(out var dynamicData, out var rushData);

        if (rushData.JustUsedCard)
            return rushData.BlueIndex;

        foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
            if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !dynamicData.Invoke<bool>("DashCorrectCheck", Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                player.MoveVExact((int) (jumpThru.Top - player.Bottom));
        }

        if (Input.Jump.Pressed
            && rushData.BlueHyperTimePassed
            && dynamicData.Get<float>("jumpGraceTimer") > 0f) {
            player.Ducking = true;
            player.StateMachine.State = 0;
            dynamicData.Invoke("SuperJump");

            return 0;
        }

        player.UpdateTrail(Color.Blue, 0.66f);

        return rushData.BlueIndex;
    }

    private static IEnumerator BlueCoroutine(this Player player) {
        yield return null;

        player.GetData(out var dynamicData, out var rushData);
        rushData.JustUsedCard = false;

        int aimX = Math.Sign(dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim")).X);

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
        player.GetData(out var dynamicData, out var rushData);

        if (rushData.JustUsedCard)
            return rushData.GreenIndex;

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

        return rushData.GreenIndex;
    }

    private static IEnumerator GreenCoroutine(this Player player) {
        yield return null;

        player.GetData(out _, out var rushData);
        rushData.JustUsedCard = false;
        player.Speed = new Vector2(0f, GREEN_FALL_SPEED);
        player.DashDir = Vector2.UnitY;
    }

    private static int RedUpdate(this Player player) {
        player.GetData(out var dynamicData, out var rushData);

        if (rushData.JustUsedCard)
            return rushData.RedIndex;

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
        }

        return rushData.RedIndex;
    }

    private static IEnumerator RedCoroutine(this Player player) {
        yield return null;

        player.GetData(out var dynamicData, out var rushData);
        rushData.JustUsedCard = false;
        player.SceneAs<Level>().Displacement.AddBurst(player.Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        rushData.RedBoostTimer = RED_BOOST_DURATION;
        rushData.RedSoundSource.Play("event:/rushHelper/game/red_boost_sustain");
        rushData.RedSoundSource.DisposeOnTransition = false;

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
        player.GetData(out var dynamicData, out var rushData);

        if (rushData.JustUsedCard)
            return rushData.WhiteIndex;

        if (rushData.WhiteRedirect) {
            var direction = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
            float dashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed").Length();

            if (direction == player.DashDir)
                dashSpeed *= WHITE_ACCELERATE_MULT;

            if (dashSpeed < WHITE_SPEED)
                dashSpeed = WHITE_SPEED;

            player.DoWhiteDash(direction, dashSpeed);
            rushData.WhiteRedirect = false;

            return rushData.WhiteIndex;
        }

        if (player.CheckUseCard()) {
            if (!player.NextCardIs(AbilityCardType.White))
                return player.UseCard();

            player.PopCard();
            dynamicData.Set("beforeDashSpeed", player.Speed);
            player.Speed = Vector2.Zero;
            player.Sprite.Scale = Vector2.One;
            player.Sprite.Play("dreamDashIn");
            player.Sprite.SetAnimationFrame(2);
            Audio.Play(SFX.char_bad_dash_red_right, player.Position);
            Celeste.Freeze(0.033f);
            rushData.WhiteRedirect = true;

            return rushData.WhiteIndex;
        }

        if (player.CanDash)
            return player.StartDash();

        if (player.DashDir.Y == 0f) {
            foreach (var jumpThru in player.Scene.Tracker.GetEntities<JumpThru>()) {
                if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f && !dynamicData.Invoke<bool>("DashCorrectCheck", Vector2.UnitY * (jumpThru.Top - player.Bottom)))
                    player.MoveVExact((int) (jumpThru.Top - player.Bottom));
            }
        }

        if (Input.Jump.Pressed && player.CanUnDuck) {
            if (player.DashDir.Y == 0f && dynamicData.Get<float>("jumpGraceTimer") > 0f) {
                player.Jump();

                return 0;
            }

            if (player.DashDir.X == 0f && player.DashDir.Y < 0f) {
                float beforeY = player.Speed.Y;

                if (dynamicData.Invoke<bool>("WallJumpCheck", 1)) {
                    dynamicData.Invoke("SuperWallJump", -1);

                    if (player.Speed.Y > beforeY)
                        player.Speed.Y = beforeY;

                    return 0;
                }

                if (dynamicData.Invoke<bool>("WallJumpCheck", -1)) {
                    dynamicData.Invoke("SuperWallJump", 1);

                    if (player.Speed.Y > beforeY)
                        player.Speed.Y = beforeY;

                    return 0;
                }
            }
            else {
                if (dynamicData.Invoke<bool>("WallJumpCheck", 1)) {
                    dynamicData.Invoke("WallJump", -1);

                    return 0;
                }

                if (dynamicData.Invoke<bool>("WallJumpCheck", -1)) {
                    dynamicData.Invoke("WallJump", 1);

                    return 0;
                }
            }
        }

        player.UpdateTrail(Color.White, 0.33f);

        return rushData.WhiteIndex;
    }

    private static IEnumerator WhiteCoroutine(this Player player) {
        yield return null;

        player.GetData(out var dynamicData, out var rushData);
        rushData.JustUsedCard = false;

        var direction = dynamicData.Invoke<Vector2>("CorrectDashPrecision", dynamicData.Get<Vector2>("lastAim"));
        var beforeDashSpeed = dynamicData.Get<Vector2>("beforeDashSpeed");
        float dashSpeed = WHITE_SPEED;

        if (direction.X != 0f && Math.Sign(direction.X) == Math.Sign(beforeDashSpeed.X) && Math.Abs(beforeDashSpeed.X) > dashSpeed)
            dashSpeed = Math.Abs(beforeDashSpeed.X);

        if (direction.Y != 0f && Math.Sign(direction.Y) == Math.Sign(beforeDashSpeed.Y) && Math.Abs(beforeDashSpeed.Y) > dashSpeed)
            dashSpeed = Math.Abs(beforeDashSpeed.Y);

        player.DoWhiteDash(direction, dashSpeed);
    }

    private static void WhiteEnd(this Player player) {
        player.GetData(out _, out var rushData);
        player.Sprite.Scale = Vector2.One;
        player.Sprite.Rotation = 0f;
        player.Sprite.Origin.Y = 32f;
        player.Sprite.Position.Y = 0f;
        player.Hair.Visible = true;
        rushData.WhiteSoundSource.Stop();
        Audio.Play(SFX.char_bad_dreamblock_exit);
        Audio.Play(SFX.game_05_redbooster_end);
    }

    private static float GetGroundJumpGraceTime(float value, Player player)
        => player.TryGetData(out _, out var rushData) && player.StateMachine.State == rushData.BlueIndex ? BLUE_HYPER_GRACE_TIME_GROUND : value;

    private static bool IsInFloorCorrectState(bool value, Player player) {
        if (value)
            return true;

        if (!player.TryGetData(out _, out var rushData))
            return false;

        int state = player.StateMachine.State;

        return state == rushData.BlueIndex
               || state == rushData.RedIndex
               || state == rushData.WhiteIndex;
    }

    private static void OnTrueCollideH(Player player) {
        if (player.TryGetData(out _, out var rushData) && player.StateMachine.State == rushData.WhiteIndex)
            player.StateMachine.State = 0;
    }

    private static void OnTrueCollideV(Player player) {
        if (!player.TryGetData(out var dynamicData, out var rushData))
            return;

        int state = player.StateMachine.State;

        if (state == rushData.GreenIndex) {
            player.Speed.X = dynamicData.Get<int>("moveX") * GREEN_LAND_SPEED;
            player.Sprite.Scale = new Vector2(1.5f, 0.75f);
            Audio.Play(SFX.game_gen_fallblock_impact, player.Position);
            Celeste.Freeze(0.05f);
            player.StateMachine.State = 0;

            var level = player.SceneAs<Level>();

            level.Particles.Emit(Player.P_SummitLandA, 12, player.BottomCenter, Vector2.UnitX * 3f, -1.5707964f);
            level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter - Vector2.UnitX * 2f, Vector2.UnitX * 2f, 3.403392f);
            level.Particles.Emit(Player.P_SummitLandB, 8, player.BottomCenter + Vector2.UnitX * 2f, Vector2.UnitX * 2f, -0.2617994f);
            level.Displacement.AddBurst(player.Center, 0.4f, 16f, 128f, 1f, Ease.QuadOut, Ease.QuadOut);

            int dashRestores = Demon.KillInRadius(player.Scene, player.Center, GREEN_LAND_KILL_RADIUS);

            player.RefillDashes(dashRestores);

            if (dashRestores >= 2)
                Audio.Play(SFX.game_10_pinkdiamond_touch, player.Position);
        }
        else if (state == rushData.WhiteIndex)
            player.StateMachine.State = 0;
    }

    private static bool IsInTransitionableState(Player player) {
        if (!player.TryGetData(out _, out var rushData))
            return false;

        int state = player.StateMachine.State;

        return state == rushData.GreenIndex || state == rushData.WhiteIndex;
    }

    private static float GetAirFriction(float value, Player player)
        => player.TryGetData(out _, out var rushData) && rushData.RedBoostTimer > 0f ? value * RED_FRICTION_MULTIPLIER : value;

    private static float GetGroundFriction(float value, Player player) {
        if (!player.TryGetData(out var dynamicData, out var rushData)
            || !player.Ducking && dynamicData.Get<int>("moveX") != Math.Sign(player.Speed.X))
            return value;

        if (rushData.Surfing)
            return 0f;

        if (rushData.RedBoostTimer > 0f)
            return value * RED_FRICTION_MULTIPLIER;

        return value;
    }

    private static float GetWallSpeedRetentionTime(float value, Player player)
        => player.TryGetData(out _, out var rushData) && rushData.RedBoostTimer > 0f ? RED_WALL_SPEED_RETENTION_TIME : value;

    private static bool IsInWallbounceState(bool value, Player player)
        => value || player.TryGetData(out _, out var rushData) && player.StateMachine.State == rushData.WhiteIndex;

    private static void Player_Update(On.Celeste.Player.orig_Update update, Player player) {
        player.GetData(out _, out var rushData);

        if (player.CollideCheck<SurfPlatform>(player.Position + Vector2.UnitY)) {
            if (rushData == null)
                player.GetOrCreateData(out _, out rushData);

            if (!rushData.Surfing) {
                Audio.Play(SFX.char_mad_water_in);
                rushData.SurfSoundSource.Play("event:/rushHelper/game/surf", "fade", MathHelper.Min(Math.Abs(player.Speed.X) / SURF_SPEED, 1f));
                rushData.Surfing = true;
            }
        }
        else if (rushData != null && rushData.Surfing) {
            rushData.SurfSoundSource.Stop();
            rushData.Surfing = false;
        }

        if (rushData == null) {
            update(player);

            return;
        }

        rushData.RedBoostTimer -= Engine.DeltaTime;

        if (rushData.RedBoostTimer < 0f)
            rushData.RedBoostTimer = 0f;

        rushData.RedLateBounceTimer -= Engine.DeltaTime;

        if (rushData.RedLateBounceTimer < 0f)
            rushData.RedLateBounceTimer = 0f;

        update(player);

        var redParticleEmitter = rushData.RedParticleEmitter;

        if (rushData.RedBoostTimer > 0f && player.Speed.Length() > 64f) {
            redParticleEmitter.Interval = 0.008f / Math.Min(player.Speed.Length() / RED_DASH_SPEED.X, 1f);
            redParticleEmitter.Position = -8f * Vector2.UnitY;
            redParticleEmitter.Start();
        }
        else if (redParticleEmitter.Active)
            redParticleEmitter.Stop();

        if (rushData.RedBoostTimer > 0f)
            player.UpdateTrail(Color.Red * Math.Min(4f * rushData.RedBoostTimer, 1f), 0.16f);
        else if (rushData.RedSoundSource.Playing)
            rushData.RedSoundSource.Stop();

        if (rushData.SurfSoundSource.Playing)
            rushData.SurfSoundSource.Param("fade", MathHelper.Min(Math.Abs(player.Speed.X) / SURF_SPEED, 1f));
    }

    private static void Player_orig_Update_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(instr => instr.MatchStfld<Player>("jumpGraceTimer"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetGroundJumpGraceTime);

        cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<Player>("get_DashAttacking"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(IsInFloorCorrectState);
    }

    private static PlayerDeadBody Player_Die(On.Celeste.Player.orig_Die die, Player player, Vector2 direction, bool evenifinvincible, bool registerdeathinstats)
        => !evenifinvincible && player.CollideCheck<RushGoal>() ? null : die(player, direction, evenifinvincible, registerdeathinstats);

    private static void Player_OnCollideH(On.Celeste.Player.orig_OnCollideH onCollideH, Player player, CollisionData data) {
        if (data.Hit is RushDashBlock dashBlock && player.TryGetData(out _, out var rushData) && (rushData.RedBoostTimer > 0f || player.StateMachine.State == rushData.BlueIndex)) {
            dashBlock.Break(player.Center, data.Direction, true, true);
            Celeste.Freeze(0.05f);
        }
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
        cursor.EmitCall(IsInCustomDash);
        cursor.Emit(OpCodes.Brtrue_S, label);

        cursor.GotoNext(MoveType.Before,
            instr => instr.MatchStfld<Player>("wallSpeedRetentionTimer"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetWallSpeedRetentionTime);

        cursor.Index = -1;
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(OnTrueCollideH);
    }

    private static void Player_OnCollideV(On.Celeste.Player.orig_OnCollideV onCollideV, Player player, CollisionData data) {
        if (data.Hit is RushDashBlock dashBlock && player.TryGetData(out _, out var rushData) && (rushData.RedBoostTimer > 0f || player.StateMachine.State == rushData.GreenIndex)) {
            dashBlock.Break(player.Center, data.Direction, true, true);
            Celeste.Freeze(0.05f);
        }
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
        cursor.EmitCall(IsInCustomDash);
        cursor.Emit(OpCodes.Brtrue_S, label);

        cursor.Index = -1;
        cursor.MoveAfterLabels();

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(OnTrueCollideV);
    }

    private static void Player_OnBoundsH(On.Celeste.Player.orig_OnBoundsH onBoundsH, Player player) {
        onBoundsH(player);

        if (player.TryGetData(out _, out var rushData) && player.StateMachine.State == rushData.WhiteIndex)
            player.StateMachine.State = 0;
    }

    private static void Player_OnBoundsV(On.Celeste.Player.orig_OnBoundsV onBoundsV, Player player) {
        onBoundsV(player);

        if (player.TryGetData(out _, out var rushData) && player.StateMachine.State == rushData.WhiteIndex)
            player.Die(Vector2.Zero);
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
        if (!player.TryGetData(out var dynamicData, out var rushData)) {
            jump(player, particles, playsfx);

            return;
        }

        if (rushData.Surfing)
            Util.PlaySound(SFX.char_mad_water_out, 2f);

        jump(player, particles, playsfx);

        if (!dynamicData.Get<bool>("dreamJump"))
            return;

        player.Speed.X += rushData.WhiteJumpSpeedReturn;
        rushData.WhiteJumpSpeedReturn = 0f;
    }

    private static void Player_WallJump(On.Celeste.Player.orig_WallJump wallJump, Player player, int dir) {
        if (!player.TryGetData(out var dynamicData, out var rushData) || rushData.RedBoostTimer == 0f) {
            wallJump(player, dir);

            return;
        }

        float beforeSpeedX = Math.Abs(player.Speed.X);
        float beforeSpeedY = player.Speed.Y;

        if (dynamicData.Get<float>("wallSpeedRetentionTimer") > 0f)
            beforeSpeedX = Math.Max(beforeSpeedX, Math.Abs(dynamicData.Get<float>("wallSpeedRetained")));

        wallJump(player, dir);

        var liftBoost = dynamicData.Get<Vector2>("LiftBoost");
        float bounceSpeed = dir * Math.Max(130f, beforeSpeedX + RED_WALL_JUMP_ADD_SPEED) + liftBoost.X;

        if (dynamicData.Get<int>("moveX") == dir)
            player.Speed.X = bounceSpeed;
        else {
            rushData.RedLateBounceTimer = RED_LATE_BOUNCE_TIME;
            rushData.RedLateBounceSpeed = bounceSpeed - player.Speed.X;
        }

        player.Speed.Y = Math.Min(-105f, beforeSpeedY) + liftBoost.Y;
        dynamicData.Set("varJumpSpeed", player.Speed.Y);
    }

    private static void Player_WallJumpCheck_il(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<Player>("get_DashAttacking"));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(IsInWallbounceState);
    }

    private static int Player_NormalUpdate(On.Celeste.Player.orig_NormalUpdate normalUpdate, Player player) {
        int nextState = normalUpdate(player);

        if (nextState != 0 || !player.TryGetData(out var dynamicData, out var rushData))
            return nextState;

        if (rushData.RedLateBounceTimer > 0f && Input.MoveX.Value == Math.Sign(rushData.RedLateBounceSpeed)) {
            rushData.RedLateBounceTimer = 0f;
            player.Speed.X += rushData.RedLateBounceSpeed;
        }

        int moveX = dynamicData.Get<int>("moveX");

        if (moveX != 0 && rushData.Surfing && dynamicData.Get<bool>("onGround") && !player.Ducking && moveX * player.Speed.X < SURF_SPEED)
            player.Speed.X = Calc.Approach(player.Speed.X, moveX * SURF_SPEED, Engine.DeltaTime * SURF_ACCELERATION);

        return 0;
    }

    private static void Player_NormalUpdate_il(ILContext il) {
        var cursor = new ILCursor(il);

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

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(500f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetGroundFriction);

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(0.65f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetAirFriction);

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(1f));

        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitCall(GetGroundFriction);
    }

    private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin dashBegin, Player player) {
        if (player.TryGetData(out _, out var rushData))
            rushData.RedBoostTimer = 0f;

        dashBegin(player);

        bool ducking = player.Ducking;

        player.Ducking = false;

        foreach (Demon entity in player.CollideAll<Demon>())
            entity.OnPlayer(player);

        player.Ducking = ducking;
    }

    private static void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite updateSprite, Player player) {
        if (!player.IsInCustomDash()) {
            updateSprite(player);

            return;
        }

        player.GetData(out _, out var rushData);

        int state = player.StateMachine.State;
        var sprite = player.Sprite;

        if (state == rushData.BlueIndex)
            sprite.Play("dash");
        else if (state == rushData.GreenIndex)
            sprite.Play("fallFast");
        else if (state == rushData.RedIndex) {
            if (player.Ducking)
                sprite.Play("duck");
            else
                sprite.Play("dash");
        }
        else if (state == rushData.WhiteIndex) {
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

    private class RushData {
        public int YellowIndex;
        public int BlueIndex;
        public int GreenIndex;
        public int RedIndex;
        public int WhiteIndex;
        public Queue<AbilityCardType> Cards = new();
        public CardInventoryIndicator CardInventoryIndicator;
        public bool JustUsedCard;
        public bool BlueHyperTimePassed;
        public float RedBoostTimer;
        public float RedLateBounceTimer;
        public float RedLateBounceSpeed;
        public SmoothParticleEmitter RedParticleEmitter;
        public SoundSource RedSoundSource;
        public bool WhiteRedirect;
        public float WhiteJumpSpeedReturn;
        public SoundSource WhiteSoundSource;
        public bool Surfing;
        public SoundSource SurfSoundSource;
    }
}