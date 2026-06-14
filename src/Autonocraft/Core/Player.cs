using System;
using System.Numerics;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public class Player
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw { get; set; } = -90f;
        public float Pitch { get; set; } = 0f;

        public float Health { get; set; } = 20f;
        public float MaxHealth { get; set; } = 20f;

        public const float Width = 0.6f;
        public const float Height = 1.8f;
        public const float EyeHeight = 1.6f;

        public const float Gravity = -32f;
        public const float WalkSpeed = 5.0f;
        public const float FlySpeed = 15.0f;
        public const float JumpForce = 9.0f;
        public const float Damping = 0.15f;

        public const float SwimSpeed = 4.5f;
        public const float MaxOxygen = 15f;
        public const float OxygenDamagePerSecond = 2f;

        public bool IsGrounded { get; private set; }
        public bool InWater { get; private set; }
        public bool HeadUnderwater { get; private set; }
        public bool OnWaterSurface { get; private set; }
        public float Oxygen { get; private set; } = MaxOxygen;
        public bool CreativeMode { get; set; } = false;
        public float CustomMoveSpeed { get; set; } = 0f;
        public bool IsAlive => Health > 0f;
        public bool JustLanded { get; private set; }
        public float FallDistance { get; private set; }

        public Action<string>? ShowToast { get; set; }

        public PlayerSkills Skills { get; } = new();
        public PlayerStatistics Stats { get; } = new();

        private void Notify(string message) => ShowToast?.Invoke(message);

        private bool _wasGrounded = true;
        private bool _wasInWater;
        private float _fallStartY;
        private float _invulnerabilityTimer;
        public const float InvulnerabilityDuration = 0.5f;

        public void ResetFallTracking()
        {
            _wasGrounded = IsGrounded;
            _wasInWater = InWater;
            _fallStartY = Position.Y;
            JustLanded = false;
            FallDistance = 0f;
        }

        public ItemStack[] Hotbar { get; } = new ItemStack[9];
        public int SelectedSlot { get; set; } = 0;

        public Player(Vector3 spawnPosition)
        {
            Position = spawnPosition;
            Velocity = Vector3.Zero;

            Hotbar[0] = ItemStack.CreateBlock(BlockType.Grass, 32);
            Hotbar[1] = ItemStack.CreateBlock(BlockType.OakLog, 16);
            Hotbar[2] = ItemStack.CreateBlock(BlockType.Dirt, 32);
            Hotbar[3] = ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood);
            Hotbar[4] = ToolRegistry.CreateStack(ToolType.Axe, ToolTier.Wood);

            for (int i = 5; i < 9; i++)
            {
                Hotbar[i] = ItemStack.Empty;
            }
        }

        public ItemStack GetSelectedStack() => Hotbar[SelectedSlot];

        public bool IsHoldingTool()
        {
            return GetSelectedStack().IsTool();
        }

        public bool CanPlaceFromSelected()
        {
            var stack = GetSelectedStack();
            if (CreativeMode && stack.IsBlock())
            {
                return true;
            }

            return stack.IsBlock() && stack.Count > 0;
        }

        public BlockType GetSelectedBlockType()
        {
            var stack = GetSelectedStack();
            return stack.IsBlock() ? stack.BlockType : BlockType.Air;
        }

        public bool UseSelectedBlock()
        {
            ref var slot = ref Hotbar[SelectedSlot];
            if (!slot.IsBlock())
            {
                return false;
            }

            if (CreativeMode)
            {
                return true;
            }

            slot.Count--;
            if (slot.Count <= 0)
            {
                slot = ItemStack.Empty;
            }

            return true;
        }

        public bool DamageSelectedTool(int amount)
        {
            if (amount <= 0 || CreativeMode)
            {
                return false;
            }

            ref var slot = ref Hotbar[SelectedSlot];
            if (!slot.IsTool())
            {
                return false;
            }

            slot.Durability -= amount;
            if (slot.Durability <= 0)
            {
                string toolName = slot.GetDisplayName();
                slot = ItemStack.Empty;
                Stats.RecordToolBroken();
                Notify($"{toolName} broke!");
                return true;
            }

            return false;
        }

        public float GetSelectedMeleeDamage()
        {
            var stack = GetSelectedStack();
            if (stack.IsTool() && ToolRegistry.TryGet(stack.ToolId, out var def) && def.ToolType == ToolType.Sword)
            {
                return def.MeleeDamage * Skills.GetBonus(PlayerSkill.Combat);
            }

            return CombatSystem.BareHandDamage;
        }

        public void GiveBlocks(BlockType blockType, int count)
        {
            AddItem(ItemStack.CreateBlock(blockType, count));
        }

        public void AddItem(ItemStack item)
        {
            if (item.IsEmpty)
            {
                return;
            }

            if (item.IsBlock())
            {
                AddBlockStack(item.BlockType, item.Count);
                return;
            }

            if (item.IsTool())
            {
                AddToolStack(item);
                return;
            }

            if (item.IsFluidContainer())
            {
                AddFluidContainerStack(item);
            }
        }

        public void AddToInventory(BlockType blockType)
        {
            if (blockType == BlockType.Air)
            {
                return;
            }

            AddBlockStack(blockType, 1);
        }

        private void AddBlockStack(BlockType blockType, int count)
        {
            if (blockType == BlockType.Air || count <= 0)
            {
                return;
            }

            int remaining = count;
            for (int i = 0; i < 9 && remaining > 0; i++)
            {
                if (Hotbar[i].IsBlock() && Hotbar[i].BlockType == blockType && Hotbar[i].Count < 64)
                {
                    int add = Math.Min(64 - Hotbar[i].Count, remaining);
                    Hotbar[i].Count += add;
                    remaining -= add;
                }
            }

            for (int i = 0; i < 9 && remaining > 0; i++)
            {
                if (Hotbar[i].IsEmpty)
                {
                    int add = Math.Min(64, remaining);
                    Hotbar[i] = ItemStack.CreateBlock(blockType, add);
                    remaining -= add;
                }
            }

            if (remaining > 0)
            {
                Notify($"Hotbar full! Lost {remaining}x {blockType}");
            }
        }

        private void AddToolStack(ItemStack tool)
        {
            for (int i = 0; i < 9; i++)
            {
                if (Hotbar[i].CanStackWith(tool))
                {
                    Hotbar[i].Count++;
                    Console.WriteLine($"[Inventory] Added {tool.GetDisplayName()} to slot {i + 1}");
                    return;
                }
            }

            for (int i = 0; i < 9; i++)
            {
                if (Hotbar[i].IsEmpty)
                {
                    Hotbar[i] = tool;
                    Console.WriteLine($"[Inventory] Added {tool.GetDisplayName()} to slot {i + 1}");
                    return;
                }
            }

            Console.WriteLine($"[Inventory] Hotbar full! Cannot collect {tool.GetDisplayName()}.");
            Notify($"Hotbar full! Cannot collect {tool.GetDisplayName()}");
        }

        private void AddFluidContainerStack(ItemStack container)
        {
            for (int i = 0; i < 9; i++)
            {
                if (Hotbar[i].IsEmpty)
                {
                    Hotbar[i] = container;
                    Console.WriteLine($"[Inventory] Added {container.GetDisplayName()} to slot {i + 1}");
                    return;
                }
            }

            Console.WriteLine($"[Inventory] Hotbar full! Cannot collect {container.GetDisplayName()}.");
            Notify($"Hotbar full! Cannot collect {container.GetDisplayName()}");
        }

        public bool TakeDamage(float amount, out bool tookDamage)
        {
            tookDamage = false;
            if (!IsAlive || _invulnerabilityTimer > 0f || amount <= 0f)
            {
                return false;
            }

            Health = Math.Max(0f, Health - amount);
            _invulnerabilityTimer = InvulnerabilityDuration;
            tookDamage = true;
            Stats.RecordDamageTaken(amount);
            return true;
        }

        public bool TakeDamage(float amount)
        {
            return TakeDamage(amount, out _);
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

        public void Update(float deltaTime, VoxelWorld world, Vector3 moveInput, bool swimUp = false, bool swimDown = false)
        {
            JustLanded = false;
            FallDistance = 0f;
            Vector3 prevPos = Position;

            if (CreativeMode)
            {
                float speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : FlySpeed;
                Velocity.Y = moveInput.Y * speed;
                Velocity.X = moveInput.X * speed;
                Velocity.Z = moveInput.Z * speed;

                Position += Velocity * deltaTime;
                IsGrounded = false;
                InWater = false;
                HeadUnderwater = false;
                OnWaterSurface = false;
            }
            else
            {
                Vector3 horizontalMove = new Vector3(moveInput.X, 0, moveInput.Z);
                if (horizontalMove != Vector3.Zero)
                {
                    horizontalMove = Vector3.Normalize(horizontalMove);
                    float speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : WalkSpeed;
                    if (InWater)
                    {
                        speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : SwimSpeed;
                    }

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

                EntityCollision.ApplyGravityAndMove(
                    ref state,
                    world,
                    deltaTime,
                    Width,
                    Height,
                    EyeHeight,
                    horizontalMove,
                    swimUp,
                    swimDown);

                Position = state.Position;
                Velocity = state.Velocity;
                IsGrounded = state.IsGrounded;
                InWater = state.InWater;
                HeadUnderwater = state.HeadUnderwater;
                OnWaterSurface = state.OnWaterSurface;

                if (HeadUnderwater)
                {
                    float prevOxygen = Oxygen;
                    Oxygen = MathF.Max(0f, Oxygen - deltaTime);
                    if (prevOxygen > 0f && Oxygen <= 0f)
                    {
                        Stats.RecordDrowning();
                    }
                }
                else
                {
                    Oxygen = MaxOxygen;
                }

                if (_wasGrounded && !IsGrounded)
                {
                    _fallStartY = Position.Y;
                }
                else if (!IsGrounded && !InWater)
                {
                    _fallStartY = MathF.Max(_fallStartY, Position.Y);
                }
                else if (!_wasInWater && InWater && !_wasGrounded)
                {
                    JustLanded = true;
                    FallDistance = MathF.Max(0f, _fallStartY - Position.Y);
                }
                else if (!_wasGrounded && IsGrounded)
                {
                    JustLanded = true;
                    FallDistance = MathF.Max(0f, _fallStartY - Position.Y);
                }

                _wasGrounded = IsGrounded;
                _wasInWater = InWater;
            }

            Stats.RecordMovement(prevPos, Position, CreativeMode, IsGrounded);
        }

        public void Jump()
        {
            if (CreativeMode)
            {
                return;
            }

            if (IsGrounded)
            {
                Velocity.Y = JumpForce;
                IsGrounded = false;
                Console.WriteLine("[Player] Jumped!");
            }
            else if (InWater && !HeadUnderwater)
            {
                Velocity.Y = JumpForce * 0.85f;
                Console.WriteLine("[Player] Jumped from water!");
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
                if (Hotbar[i].IsEmpty)
                {
                    sb.Append($"[{marker}{i + 1}: -]");
                }
                else if (Hotbar[i].IsTool())
                {
                    sb.Append($"[{marker}{i + 1}: {Hotbar[i].GetDisplayName()} ({Hotbar[i].Durability})]");
                }
                else
                {
                    sb.Append($"[{marker}{i + 1}: {Hotbar[i].BlockType} ({Hotbar[i].Count})]");
                }

                if (i < 8) sb.Append(" ");
            }

            return sb.ToString();
        }
    }
}
