using System;
using System.Numerics;
using Autonocraft.Entities;
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

        public bool BlocksMiningThisFrame { get; private set; }

        public void Update(
            float deltaTime,
            VoxelWorld world,
            Player player,
            AnimalManager animals,
            BlockInteractionSystem blockInteraction,
            Vector3 cameraPos,
            Vector3 cameraFront,
            bool leftHeld,
            bool leftPressed)
        {
            BlocksMiningThisFrame = false;
            player.UpdateInvulnerability(deltaTime);

            if (_attackCooldown > 0f)
            {
                _attackCooldown = Math.Max(0f, _attackCooldown - deltaTime);
            }

            HandleFallDamage(player);

            if (!player.IsAlive)
            {
                return;
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

            var (blockPos, _, _, blockDistance) = BlockInteractionSystem.Raycast(
                world,
                cameraPos,
                cameraFront,
                BlockInteractionSystem.RaycastRange);

            if (blockPos.HasValue && blockDistance <= entityDistance)
            {
                return;
            }

            PerformMeleeAttack(player, animals, blockInteraction, targetAnimal, cameraPos);
        }

        public bool TryInstantAttack(
            VoxelWorld world,
            Player player,
            AnimalManager animals,
            BlockInteractionSystem blockInteraction,
            Vector3 cameraPos,
            Vector3 cameraFront)
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

            var (blockPos, _, _, blockDistance) = BlockInteractionSystem.Raycast(
                world,
                cameraPos,
                cameraFront,
                BlockInteractionSystem.RaycastRange);

            if (blockPos.HasValue && blockDistance <= entityDistance)
            {
                return false;
            }

            PerformMeleeAttack(player, animals, blockInteraction, targetAnimal, cameraPos);
            return true;
        }

        private void PerformMeleeAttack(
            Player player,
            AnimalManager animals,
            BlockInteractionSystem blockInteraction,
            Animal targetAnimal,
            Vector3 attackerPos)
        {
            _attackCooldown = AttackCooldownSeconds;
            BlocksMiningThisFrame = true;

            targetAnimal.TakeDamage(BareHandDamage, attackerPos);
            blockInteraction.TriggerCrosshairFlash();

            Console.WriteLine(
                $"[Combat] Hit {targetAnimal.Type} for {BareHandDamage:F0} damage ({targetAnimal.Health:F0}/{targetAnimal.MaxHealth:F0} HP)");

            ApplyRetaliation(player, targetAnimal, attackerPos);

            if (!targetAnimal.IsAlive)
            {
                animals.KillAnimal(targetAnimal);
                Console.WriteLine($"[Combat] {targetAnimal.Type} died.");
            }
        }

        private static void ApplyRetaliation(Player player, Animal animal, Vector3 attackerPos)
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

            if (player.TakeDamage(retaliation))
            {
                Console.WriteLine($"[Combat] Player took {retaliation:F0} retaliation damage ({player.Health:F0}/{player.MaxHealth:F0} HP)");
            }
        }

        private static void HandleFallDamage(Player player)
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

            if (player.TakeDamage(damage))
            {
                Console.WriteLine(
                    $"[Combat] Fall damage: {damage:F0} from {player.FallDistance:F1} blocks ({player.Health:F0}/{player.MaxHealth:F0} HP)");
            }
        }

        public static void RespawnPlayer(VoxelWorld world, Player player)
        {
            var spawnPos = Player.FindSafeSpawnPosition(world, AutonocraftGame.DefaultSpawnX, AutonocraftGame.DefaultSpawnZ);
            player.Position = spawnPos;
            player.Velocity = Vector3.Zero;
            player.Health = player.MaxHealth;
            Console.WriteLine($"[Combat] Player respawned at ({spawnPos.X:F1}, {spawnPos.Y:F1}, {spawnPos.Z:F1})");
        }
    }
}
