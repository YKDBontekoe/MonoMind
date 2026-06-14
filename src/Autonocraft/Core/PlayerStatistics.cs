using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Autonocraft.Entities;

namespace Autonocraft.Core
{
    public sealed class PlayerStatistics
    {
        public const float StepsPerBlock = 1.4285714f;

        public double TotalPlayTimeSeconds { get; set; }
        public int SessionCount { get; set; }
        public float DistanceWalked { get; set; }
        public int StepsWalked { get; set; }
        public float MaxAltitude { get; set; }
        public float DistanceFlown { get; set; }
        public int AnimalsKilled { get; set; }
        public int SheepKilled { get; set; }
        public int PigKilled { get; set; }
        public int ChickenKilled { get; set; }
        public float DamageDealt { get; set; }
        public float DamageTaken { get; set; }
        public int PlayerDeaths { get; set; }
        public int BlocksBroken { get; set; }
        public int BlocksPlaced { get; set; }
        public int ToolsBroken { get; set; }
        public int FallDamageEvents { get; set; }
        public int TimesDrowned { get; set; }
        public int ItemsCrafted { get; set; }

        public void RecordPlayTime(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                TotalPlayTimeSeconds += deltaTime;
            }
        }

        public void RecordMovement(Vector3 prevPos, Vector3 newPos, bool flying, bool grounded)
        {
            float horizontal = MathF.Sqrt(
                (newPos.X - prevPos.X) * (newPos.X - prevPos.X) +
                (newPos.Z - prevPos.Z) * (newPos.Z - prevPos.Z));

            if (horizontal > 0.001f)
            {
                if (flying)
                {
                    DistanceFlown += horizontal;
                }
                else
                {
                    DistanceWalked += horizontal;
                    if (grounded)
                    {
                        StepsWalked += Math.Max(1, (int)MathF.Round(horizontal * StepsPerBlock));
                    }
                }
            }

            MaxAltitude = MathF.Max(MaxAltitude, newPos.Y);
        }

        public void RecordAnimalKill(AnimalType type, float damageDealt)
        {
            AnimalsKilled++;
            DamageDealt += damageDealt;
            switch (type)
            {
                case AnimalType.Sheep:
                    SheepKilled++;
                    break;
                case AnimalType.Pig:
                    PigKilled++;
                    break;
                case AnimalType.Chicken:
                    ChickenKilled++;
                    break;
            }
        }

        public void RecordMeleeDamage(float damage)
        {
            if (damage > 0f)
            {
                DamageDealt += damage;
            }
        }

        public void RecordDamageTaken(float amount)
        {
            if (amount > 0f)
            {
                DamageTaken += amount;
            }
        }

        public void RecordDeath() => PlayerDeaths++;

        public void RecordBlockBroken() => BlocksBroken++;

        public void RecordBlockPlaced() => BlocksPlaced++;

        public void RecordToolBroken() => ToolsBroken++;

        public void RecordFallDamage() => FallDamageEvents++;

        public void RecordDrowning() => TimesDrowned++;

        public void RecordItemCrafted() => ItemsCrafted++;

        public void RecordSessionStart() => SessionCount++;

        public PlayerStatistics Clone()
        {
            return new PlayerStatistics
            {
                TotalPlayTimeSeconds = TotalPlayTimeSeconds,
                SessionCount = SessionCount,
                DistanceWalked = DistanceWalked,
                StepsWalked = StepsWalked,
                MaxAltitude = MaxAltitude,
                DistanceFlown = DistanceFlown,
                AnimalsKilled = AnimalsKilled,
                SheepKilled = SheepKilled,
                PigKilled = PigKilled,
                ChickenKilled = ChickenKilled,
                DamageDealt = DamageDealt,
                DamageTaken = DamageTaken,
                PlayerDeaths = PlayerDeaths,
                BlocksBroken = BlocksBroken,
                BlocksPlaced = BlocksPlaced,
                ToolsBroken = ToolsBroken,
                FallDamageEvents = FallDamageEvents,
                TimesDrowned = TimesDrowned,
                ItemsCrafted = ItemsCrafted
            };
        }

        public static PlayerStatistics Aggregate(IEnumerable<PlayerStatistics> stats)
        {
            var result = new PlayerStatistics();
            foreach (var stat in stats)
            {
                result.TotalPlayTimeSeconds += stat.TotalPlayTimeSeconds;
                result.SessionCount += stat.SessionCount;
                result.DistanceWalked += stat.DistanceWalked;
                result.StepsWalked += stat.StepsWalked;
                result.MaxAltitude = MathF.Max(result.MaxAltitude, stat.MaxAltitude);
                result.DistanceFlown += stat.DistanceFlown;
                result.AnimalsKilled += stat.AnimalsKilled;
                result.SheepKilled += stat.SheepKilled;
                result.PigKilled += stat.PigKilled;
                result.ChickenKilled += stat.ChickenKilled;
                result.DamageDealt += stat.DamageDealt;
                result.DamageTaken += stat.DamageTaken;
                result.PlayerDeaths += stat.PlayerDeaths;
                result.BlocksBroken += stat.BlocksBroken;
                result.BlocksPlaced += stat.BlocksPlaced;
                result.ToolsBroken += stat.ToolsBroken;
                result.FallDamageEvents += stat.FallDamageEvents;
                result.TimesDrowned += stat.TimesDrowned;
                result.ItemsCrafted += stat.ItemsCrafted;
            }

            return result;
        }

        public static string FormatDuration(double seconds)
        {
            if (seconds < 60d)
            {
                return $"{(int)seconds}s";
            }

            int totalMinutes = (int)(seconds / 60d);
            if (totalMinutes < 60)
            {
                int secs = (int)seconds % 60;
                return secs > 0 ? $"{totalMinutes}m {secs}s" : $"{totalMinutes}m";
            }

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        public static string FormatDistance(float blocks)
        {
            if (blocks >= 1000f)
            {
                return $"{blocks / 1000f:0.#} km";
            }

            return $"{(int)blocks} m";
        }

        public static string FormatCount(int count) =>
            count.ToString("N0", CultureInfo.InvariantCulture);
    }
}
