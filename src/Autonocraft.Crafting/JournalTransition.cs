namespace Autonocraft.Crafting
{
    public sealed class JournalTransition
    {
        private enum Mode
        {
            None,
            FadeIn,
            FadeOut,
            FadeInSlideUp
        }

        private Mode _mode = Mode.None;
        private float _duration;
        private float _elapsed;
        private float _slideDistance;

        public float Alpha { get; private set; } = 1f;
        public float OffsetY { get; private set; }
        public bool IsComplete => _mode == Mode.None || _elapsed >= _duration;
        public bool IsAnimating => _mode != Mode.None;

        public void BeginFadeIn(float duration = 0.25f)
        {
            _mode = Mode.FadeIn;
            _duration = Math.Max(0.01f, duration);
            _elapsed = 0f;
            _slideDistance = 0f;
            Alpha = 0f;
            OffsetY = 0f;
        }

        public void BeginFadeOut(float duration = 0.25f)
        {
            _mode = Mode.FadeOut;
            _duration = Math.Max(0.01f, duration);
            _elapsed = 0f;
            _slideDistance = 0f;
            Alpha = 1f;
            OffsetY = 0f;
        }

        public void BeginFadeInSlideUp(float duration = 0.2f, float slideDistance = 12f)
        {
            _mode = Mode.FadeInSlideUp;
            _duration = Math.Max(0.01f, duration);
            _elapsed = 0f;
            _slideDistance = slideDistance;
            Alpha = 0f;
            OffsetY = slideDistance;
        }

        public void SnapHidden()
        {
            _mode = Mode.None;
            _elapsed = 0f;
            Alpha = 0f;
            OffsetY = 0f;
        }

        public void Update(float dt)
        {
            if (_mode == Mode.None)
            {
                return;
            }

            _elapsed += dt;
            float t = Math.Clamp(_elapsed / _duration, 0f, 1f);
            float eased = 1f - (1f - t) * (1f - t);

            switch (_mode)
            {
                case Mode.FadeIn:
                    Alpha = eased;
                    OffsetY = 0f;
                    break;
                case Mode.FadeOut:
                    Alpha = 1f - eased;
                    OffsetY = 0f;
                    break;
                case Mode.FadeInSlideUp:
                    Alpha = eased;
                    OffsetY = _slideDistance * (1f - eased);
                    break;
            }

            if (t >= 1f)
            {
                if (_mode == Mode.FadeOut)
                {
                    Alpha = 0f;
                }
                else
                {
                    Alpha = 1f;
                    OffsetY = 0f;
                }

                _mode = Mode.None;
            }
        }
    }
}
