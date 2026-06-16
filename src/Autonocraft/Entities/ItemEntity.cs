using System;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Engine.Audio;

namespace Autonocraft.Entities
{
    public class ItemEntity
    {
        public int Id { get; }
        public ItemStack Item { get; }
        public Vector3 Position;
        public Vector3 Velocity;
        public bool IsGrounded;
        
        public float Age { get; private set; }
        public float HoverTimer { get; private set; }
        public bool ReadyForRemoval { get; private set; }
        
        public const float Width = 0.25f;
        public const float Height = 0.25f;
        private const float Gravity = -22f; // Slower falling for dropped items than player
        private float _pickupDelay = 0.5f;

        public ItemEntity(ItemStack item, Vector3 position, int id)
        {
            Item = item;
            Position = position;
            Id = id;
            HoverTimer = (float)(Random.Shared.NextDouble() * Math.PI * 2.0); // Randomize phase so they don't bob in sync
            
            // Pop out randomly slightly upward and outward
            Velocity = new Vector3(
                (float)(Random.Shared.NextDouble() - 0.5) * 2.5f,
                3.5f, 
                (float)(Random.Shared.NextDouble() - 0.5) * 2.5f
            );
        }

        public void Update(float deltaTime, VoxelWorld world, Vector3 playerPos, Player player, Action<SfxKind>? playSfx = null)
        {
            Age += deltaTime;
            HoverTimer += deltaTime;
            if (_pickupDelay > 0f)
            {
                _pickupDelay -= deltaTime;
            }

            if (!IsGrounded)
            {
                Velocity.Y += Gravity * deltaTime;
                if (Velocity.Y < -15f) Velocity.Y = -15f;
            }
            else
            {
                Velocity.X *= MathF.Pow(0.05f, deltaTime);
                Velocity.Z *= MathF.Pow(0.05f, deltaTime);
                Velocity.Y = 0f;
            }

            var state = new EntityCollisionState
            {
                Position = Position,
                Velocity = Velocity,
                IsGrounded = IsGrounded
            };

            EntityCollision.ApplyGravityAndMove(
                ref state,
                world,
                deltaTime,
                Width,
                Height,
                Height * 0.5f,
                new Vector3(Velocity.X, 0f, Velocity.Z),
                swimUp: false,
                swimDown: false
            );

            Position = state.Position;
            Velocity = state.Velocity;
            IsGrounded = state.IsGrounded;

            // Pickup attraction/collection logic
            if (_pickupDelay <= 0f && player.IsAlive)
            {
                // Attract range: 2.0 meters, pickup range: 0.65 meters
                float dist = Vector3.Distance(Position, playerPos + new Vector3(0f, Player.EyeHeight * 0.4f, 0f));
                if (dist < 2.0f)
                {
                    Vector3 toPlayer = Vector3.Normalize((playerPos + new Vector3(0f, Player.EyeHeight * 0.4f, 0f)) - Position);
                    float pullSpeed = 6.0f * (2.0f - dist);
                    Position += toPlayer * pullSpeed * deltaTime;
                    
                    if (dist < 0.65f)
                    {
                        if (player.AddItem(Item))
                        {
                            ReadyForRemoval = true;
                            playSfx?.Invoke(SfxKind.Pop);
                        }
                    }
                }
            }
        }
    }
}
