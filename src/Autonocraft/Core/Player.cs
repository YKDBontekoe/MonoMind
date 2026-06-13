using System;
using System.Numerics;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public class Player
    {
        public Vector3 Position; // Bottom-center position of player's feet
        public Vector3 Velocity;
        public float Yaw { get; set; } = -90f;
        public float Pitch { get; set; } = 0f;

        // Player health
        public float Health { get; set; } = 20f;
        public float MaxHealth { get; set; } = 20f;

        // Player physical dimensions
        public const float Width = 0.6f;
        public const float Height = 1.8f;
        public const float EyeHeight = 1.6f;

        // Movement constants
        public const float Gravity = -32f; // acceleration in blocks/sec^2
        public const float WalkSpeed = 5.0f;
        public const float FlySpeed = 15.0f;
        public const float JumpForce = 9.0f;
        public const float Damping = 0.15f; // friction damping per second

        public bool IsGrounded { get; private set; }
        public bool FlyingMode { get; set; } = false;
        public float CustomMoveSpeed { get; set; } = 0f;
        public bool IsAlive => Health > 0f;
        public bool JustLanded { get; private set; }
        public float FallDistance { get; private set; }

        private bool _wasGrounded = true;
        private float _fallStartY;
        private float _invulnerabilityTimer;
        public const float InvulnerabilityDuration = 0.5f;

        public void ResetFallTracking()
        {
            _wasGrounded = IsGrounded;
            JustLanded = false;
            FallDistance = 0f;
        }

        // Inventory: 9 slots
        public struct InventorySlot
        {
            public BlockType Type;
            public int Count;
        }
        
        public readonly InventorySlot[] Hotbar = new InventorySlot[9];
        public int SelectedSlot { get; set; } = 0;

        public Player(Vector3 spawnPosition)
        {
            Position = spawnPosition;
            Velocity = Vector3.Zero;

            // Initialize inventory with some default items
            Hotbar[0] = new InventorySlot { Type = BlockType.Grass, Count = 64 };
            Hotbar[1] = new InventorySlot { Type = BlockType.OakLog, Count = 64 };
            Hotbar[2] = new InventorySlot { Type = BlockType.Stone, Count = 64 };
            Hotbar[3] = new InventorySlot { Type = BlockType.Dirt, Count = 64 };
            
            // Other slots are Air (empty)
            for (int i = 4; i < 9; i++)
            {
                Hotbar[i] = new InventorySlot { Type = BlockType.Air, Count = 0 };
            }
        }

        public BlockType GetSelectedBlockType()
        {
            return Hotbar[SelectedSlot].Type;
        }

        public bool UseSelectedBlock()
        {
            if (Hotbar[SelectedSlot].Type == BlockType.Air || Hotbar[SelectedSlot].Count <= 0)
            {
                return false;
            }

            Hotbar[SelectedSlot].Count--;
            if (Hotbar[SelectedSlot].Count == 0)
            {
                Hotbar[SelectedSlot].Type = BlockType.Air;
            }
            return true;
        }

        public void GiveBlocks(BlockType blockType, int count)
        {
            if (blockType == BlockType.Air || count <= 0) return;

            int remaining = count;
            for (int i = 0; i < 9 && remaining > 0; i++)
            {
                if (Hotbar[i].Type == blockType && Hotbar[i].Count < 64)
                {
                    int add = Math.Min(64 - Hotbar[i].Count, remaining);
                    Hotbar[i].Count += add;
                    remaining -= add;
                }
            }

            for (int i = 0; i < 9 && remaining > 0; i++)
            {
                if (Hotbar[i].Type == BlockType.Air)
                {
                    int add = Math.Min(64, remaining);
                    Hotbar[i] = new InventorySlot { Type = blockType, Count = add };
                    remaining -= add;
                }
            }
        }

        public void AddToInventory(BlockType blockType)
        {
            if (blockType == BlockType.Air) return;

            // First search if we already have this block in hotbar
            for (int i = 0; i < 9; i++)
            {
                if (Hotbar[i].Type == blockType && Hotbar[i].Count < 64)
                {
                    Hotbar[i].Count++;
                    Console.WriteLine($"[Inventory] Added {blockType} to Slot {i+1} (Count: {Hotbar[i].Count})");
                    return;
                }
            }

            // Find first empty slot
            for (int i = 0; i < 9; i++)
            {
                if (Hotbar[i].Type == BlockType.Air)
                {
                    Hotbar[i] = new InventorySlot { Type = blockType, Count = 1 };
                    Console.WriteLine($"[Inventory] Added {blockType} to Slot {i+1} (Count: 1)");
                    return;
                }
            }

            Console.WriteLine($"[Inventory] Hotbar full! Cannot collect {blockType}.");
        }

        public bool TakeDamage(float amount)
        {
            if (!IsAlive || _invulnerabilityTimer > 0f || amount <= 0f)
            {
                return false;
            }

            Health = Math.Max(0f, Health - amount);
            _invulnerabilityTimer = InvulnerabilityDuration;
            return true;
        }

        public void UpdateInvulnerability(float deltaTime)
        {
            if (_invulnerabilityTimer > 0f)
            {
                _invulnerabilityTimer = Math.Max(0f, _invulnerabilityTimer - deltaTime);
            }
        }

        public void ClearInvulnerability()
        {
            _invulnerabilityTimer = 0f;
        }

        public void Update(float deltaTime, VoxelWorld world, Vector3 moveInput)
        {
            JustLanded = false;
            FallDistance = 0f;

            if (FlyingMode)
            {
                float speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : FlySpeed;
                // In flying mode, move along input direction directly
                Velocity.Y = moveInput.Y * speed;
                Velocity.X = moveInput.X * speed;
                Velocity.Z = moveInput.Z * speed;

                Position += Velocity * deltaTime;
                IsGrounded = false;
            }
            else
            {
                Vector3 horizontalMove = new Vector3(moveInput.X, 0, moveInput.Z);
                if (horizontalMove != Vector3.Zero)
                {
                    horizontalMove = Vector3.Normalize(horizontalMove);
                    float speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : WalkSpeed;
                    horizontalMove *= speed;
                }
                else
                {
                    Velocity.X *= MathF.Pow(Damping, deltaTime * 10f);
                    Velocity.Z *= MathF.Pow(Damping, deltaTime * 10f);
                    if (MathF.Abs(Velocity.X) < 0.01f) Velocity.X = 0;
                    if (MathF.Abs(Velocity.Z) < 0.01f) Velocity.Z = 0;
                    horizontalMove = new Vector3(Velocity.X, 0, Velocity.Z);
                }

                var state = new EntityCollisionState
                {
                    Position = Position,
                    Velocity = Velocity,
                    IsGrounded = IsGrounded
                };

                EntityCollision.ApplyGravityAndMove(ref state, world, deltaTime, Width, Height, horizontalMove);

                Position = state.Position;
                Velocity = state.Velocity;
                IsGrounded = state.IsGrounded;

                if (_wasGrounded && !IsGrounded)
                {
                    _fallStartY = Position.Y;
                }
                else if (!IsGrounded)
                {
                    _fallStartY = MathF.Max(_fallStartY, Position.Y);
                }
                else if (!_wasGrounded && IsGrounded)
                {
                    JustLanded = true;
                    FallDistance = MathF.Max(0f, _fallStartY - Position.Y);
                }

                _wasGrounded = IsGrounded;
            }
        }

        public void Jump()
        {
            if (IsGrounded && !FlyingMode)
            {
                Velocity.Y = JumpForce;
                IsGrounded = false;
                Console.WriteLine("[Player] Jumped!");
            }
        }

        public bool Intersects(int x, int y, int z)
        {
            float minX = Position.X - Width / 2f;
            float maxX = Position.X + Width / 2f;
            float minY = Position.Y;
            float maxY = Position.Y + Height;
            float minZ = Position.Z - Width / 2f;
            float maxZ = Position.Z + Width / 2f;

            // A block at (x,y,z) has bounding box [x, x+1] x [y, y+1] x [z, z+1]
            return (minX < x + 1 && maxX > x) &&
                   (minY < y + 1 && maxY > y) &&
                   (minZ < z + 1 && maxZ > z);
        }

        public static bool IsSpaceClearAt(VoxelWorld world, Vector3 position)
        {
            return EntityCollision.IsSpaceClearAt(world, position, Width, Height);
        }

        public static Vector3 FindSafeSpawnPosition(VoxelWorld world, int x, int z)
        {
            float spawnX = x + 0.5f;
            float spawnZ = z + 0.5f;
            int surfaceY = world.GetHighestSolidY(x, z);
            if (surfaceY < 0)
            {
                surfaceY = 26;
            }

            for (int offset = 0; offset < 16; offset++)
            {
                float spawnY = surfaceY + 1f + offset;
                var candidate = new Vector3(spawnX, spawnY, spawnZ);
                if (IsSpaceClearAt(world, candidate))
                {
                    return candidate;
                }
            }

            return new Vector3(spawnX, surfaceY + 1f, spawnZ);
        }

        public string GetInventoryHUD()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 9; i++)
            {
                string marker = (i == SelectedSlot) ? "*" : "";
                if (Hotbar[i].Type == BlockType.Air)
                {
                    sb.Append($"[{marker}{i+1}: -]");
                }
                else
                {
                    sb.Append($"[{marker}{i+1}: {Hotbar[i].Type} ({Hotbar[i].Count})]");
                }
                if (i < 8) sb.Append(" ");
            }
            return sb.ToString();
        }
    }
}
