using System;

namespace Autonocraft.World.Structures
{
    public sealed class StructureRng
    {
        private uint _state;

        public StructureRng(int seed)
        {
            _state = (uint)seed;
            if (_state == 0)
            {
                _state = 1;
            }
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                return 0;
            }

            return (int)(NextUInt() % (uint)maxExclusive);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public float NextFloat()
        {
            return NextUInt() / ((float)uint.MaxValue + 1f);
        }

        public int Pick(params int[] options)
        {
            if (options == null || options.Length == 0)
            {
                throw new ArgumentException("Pick requires at least one option.", nameof(options));
            }

            return options[NextInt(options.Length)];
        }

        private uint NextUInt()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }
        }
    }
}
