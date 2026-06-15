using System;
using System.Numerics;
using Autonocraft.Entities;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Engine.Audio;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public class CombatSystem
    {
        public const float MeleeRange = 3f;
        public const float BareHandDamage = 1f;
        public const float AttackCooldownSeconds = 0.5f;
        public const float RetaliationRange = 2.5f;
        public const float FallDamageThreshold = 3f;

        private float _attackCooldown;

        public Action<string>? ShowToast { get; set; }
        public Action<SfxKind, float>? PlaySfx { get; set; }

        public bool BlocksMiningThisFrame { get; private set; }

        public void Update(
            float deltaTime,
            VoxelWorld world,
            Player player,
            AnimalManager animals,
            BlockInteractionSystem blockInteraction,
            ParticleSystem particles,
            InteractionAnimator animator,
            Vector3 cameraPos,
            Vector3 cameraFront,
            bool leftHeld,
            bool leftPressed,
            BlockRaycastHit? solidRayHit = null)
        {
            BlocksMiningThisFrame = false;
            player.UpdateInvulnerability(deltaTime);

            HandleFallDamage(player, world, animator);
            HandleDrowning(deltaTime, player, animator);

            bool isMoving = player.Velocity.X * player.Velocity.X + player.Velocity.Z * player.Velocity.Z > 0.5f;
            player.TickHunger(deltaTime, isMoving, blockInteraction.IsMining);

            if (!player.IsAlive)
            {
                return;
            }

            if (_attackCooldown > 0f)
            {
                _attackCooldown = Math.Max(0f, _attackCooldown - deltaTime);
            }

            bool wantsAttack = leftPressed || (leftHeld && _attackCooldown <= 0f);
            if (!wantsAttack)
            {
                return;
            }

            if (_attackCooldown > 0f)
            {
                return;
            }

            var (targetAnimal, entityDistance) = animals.RaycastTarget(
                cameraPos,
                cameraFront,
                BlockInteractionSystem.RaycastRange);

            if (targetAnimal == null || entityDistance > MeleeRange)
            {
                return;
            }

            var rayHit = solidRayHit ?? BlockInteractionSystem.RaycastSolidHit(
                world,
                cameraPos,
                cameraFront,
                BlockInteractionSystem.RaycastRange);

            if (rayHit.HasHit && rayHit.Distance <= entityDistance)
            {
                return;
            }

            PerformMeleeAttack(player, animals, blockInteraction, particles, animator, targetAnimal, cameraPos, cameraFront);
        }

        public bool TryInstantAttack(
            VoxelWorld world,
            Player player,
            AnimalManager animals,
            BlockInteractionSystem blockInteraction,
            ParticleSystem particles,
            InteractionAnimator animator,
            Vector3 cameraPos,
            Vector3 cameraFront,
            BlockRaycastHit? solidRayHit = null)
        {
            if (!player.IsAlive)
            {
                return false;
            }

            var (targetAnimal, entityDistance) = animals.RaycastTarget(
                cameraPos,
                cameraFront,
                BlockInteractionSystem.RaycastRange);

            if (targetAnimal == null || entityDistance > MeleeRange)
            {
                return false;
            }

            var rayHit = solidRayHit ?? BlockInteractionSystem.RaycastSolidHit(
                world,
                cameraPos,
                cameraFront,
                BlockInteractionSystem.RaycastRange);

            if (rayHit.HasHit && rayHit.Distance <= entityDistance)
            {
                return false;
            }

            PerformMeleeAttack(player, animals, blockInteraction, particles, animator, targetAnimal, cameraPos, cameraFront);
            return true;
        }

        private void PerformMeleeAttack(
            Player player,
            AnimalManager animals,
            BlockInteractionSystem blockInteraction,
            ParticleSystem particles,
            InteractionAnimator animator,
            Animal targetAnimal,
            Vector3 attackerPos,
            Vector3 hitDirection)
        {
            _attackCooldown = AttackCooldownSeconds;
            BlocksMiningThisFrame = true;

            float damage = player.GetSelectedMeleeDamage();
            targetAnimal.TakeDamage(damage, attackerPos);
            bool toolBroke = player.DamageSelectedTool(1);
            blockInteraction.TriggerCrosshairFlash();
            blockInteraction.TriggerMeleeCrosshair();
            animator.TriggerSwing(SwingKind.Melee);
            PlaySfx?.Invoke(SfxKind.MeleeHit, 1f);

            var hitCenter = targetAnimal.Position + new Vector3(0f, targetAnimal.Stats.Height * 0.5f, 0f);
            particles.SpawnMeleeHit(hitCenter, targetAnimal.Type, hitDirection);

            Console.WriteLine(
                $"[Combat] Hit {targetAnimal.Type} for {damage:F0} damage ({targetAnimal.Health:F0}/{targetAnimal.MaxHealth:F0} HP)");

            ApplyRetaliation(player, targetAnimal, attackerPos, animator);

            if (!targetAnimal.IsAlive)
            {
                player.Stats.RecordAnimalKill(targetAnimal.Type, damage);
                var loot = AnimalLoot.For(targetAnimal.Type);
                if (!loot.IsEmpty)
                {
                    player.AddItem(loot);
                }
                if (player.Skills.AddXp(PlayerSkill.Combat, 10f))
                {
                    ShowToast?.Invoke($"Combat level {player.Skills.GetLevel(PlayerSkill.Combat)}!");
                }
                particles.SpawnAnimalDeath(hitCenter, targetAnimal.Type);
                targetAnimal.BeginDeathAnimation();
                PlaySfx?.Invoke(SfxKind.AnimalDeath, 1f);
                Console.WriteLine($"[Combat] {targetAnimal.Type} died.");
            }
            else
            {
                player.Stats.RecordMeleeDamage(damage);
            }

            if (toolBroke)
            {
                animator.TriggerInvalidAction();
                PlaySfx?.Invoke(SfxKind.ToolBreak, 1f);
                particles.SpawnToolBreak(attackerPos + hitDirection * 0.5f);
            }
        }

        public void HandleLandingEffects(Player player, ParticleSystem particles, InteractionAnimator animator)
        {
            if (player.FlyingMode || !player.JustLanded)
            {
                return;
            }

            particles.SpawnFallDust(player.Position, player.FallDistance);
            animator.TriggerLand(player.FallDistance);
        }

        private static void ApplyRetaliation(Player player, Animal animal, Vector3 attackerPos, InteractionAnimator animator)
        {
            float retaliation = animal.Stats.RetaliationDamage;
            if (retaliation <= 0f)
            {
                return;
            }

            float distSq = Vector3.DistanceSquared(animal.Position, attackerPos);
            if (distSq > RetaliationRange * RetaliationRange)
            {
                return;
            }

            if (player.TakeDamage(retaliation, out _))
            {
                Console.WriteLine($"[Combat] Player took {retaliation:F0} retaliation damage ({player.Health:F0}/{player.MaxHealth:F0} HP)");
                animator.TriggerDamage(0.7f);
            }
        }

        private void HandleFallDamage(Player player, VoxelWorld world, InteractionAnimator animator)
        {
            if (player.FlyingMode || !player.JustLanded)
            {
                return;
            }

            float damage = MathF.Max(0f, player.FallDistance - FallDamageThreshold);
            if (damage <= 0f)
            {
                return;
            }

            if (WaterQuery.IsLandingInWater(world, player.Position))
            {
                damage = MathF.Min(damage, 2f);
            }

            float flashStrength = damage >= 3f ? 1.2f : 0.8f;
            if (player.TakeDamage(damage, out _))
            {
                player.Stats.RecordFallDamage();
                Console.WriteLine(
                    $"[Combat] Fall damage: {damage:F0} from {player.FallDistance:F1} blocks ({player.Health:F0}/{player.MaxHealth:F0} HP)");
                animator.TriggerDamage(flashStrength);
                PlaySfx?.Invoke(SfxKind.PlayerHurt, 1f);
            }
        }

        private static void HandleDrowning(float deltaTime, Player player, InteractionAnimator animator)
        {
            if (player.FlyingMode || !player.HeadUnderwater || player.Oxygen > 0f)
            {
                return;
            }

            float damage = Player.OxygenDamagePerSecond * deltaTime;
            if (player.TakeDamage(damage, out _))
            {
                animator.TriggerDamage(0.5f);
            }
        }

        public static void RespawnPlayer(VoxelWorld world, Player player, int spawnX = GameConstants.DefaultSpawnX, int spawnZ = GameConstants.DefaultSpawnZ)
        {
            var spawnPos = Player.FindSafeSpawnPosition(world, spawnX, spawnZ);
            player.Position = spawnPos;
            player.RestoreRespawnVitals();
            Console.WriteLine($"[Combat] Player respawned at ({spawnPos.X:F1}, {spawnPos.Y:F1}, {spawnPos.Z:F1})");
        }
    }
}
