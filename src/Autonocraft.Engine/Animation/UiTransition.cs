using System;
using Autonocraft.Domain.Core;

namespace Autonocraft.Engine.Animation
{
    public enum UiTransitionMode
    {
        None,
        FadeIn,
        FadeOut,
        FadeInSlideUp
    }

    public class UiTransition
    {
        private UiTransitionMode _mode = UiTransitionMode.None;
        private float _duration;
        private float _elapsed;
        private float _slideDistance;

        public float Alpha { get; private set; } = 1f;
        public float OffsetY { get; private set; }
        public bool IsComplete => _mode == UiTransitionMode.None || _elapsed >= _duration;

        public void BeginFadeIn(float duration = 0.25f)
        {
            _mode = UiTransitionMode.FadeIn;
            _duration = Math.Max(0.01f, duration);
            _elapsed = 0f;
            _slideDistance = 0f;
            Alpha = 0f;
            OffsetY = 0f;
        }

        public void BeginFadeOut(float duration = 0.25f)
        {
            _mode = UiTransitionMode.FadeOut;
            _duration = Math.Max(0.01f, duration);
            _elapsed = 0f;
            _slideDistance = 0f;
            Alpha = 1f;
            OffsetY = 0f;
        }

        public void BeginFadeInSlideUp(float duration = 0.2f, float slideDistance = 12f)
        {
            _mode = UiTransitionMode.FadeInSlideUp;
            _duration = Math.Max(0.01f, duration);
            _elapsed = 0f;
            _slideDistance = slideDistance;
            Alpha = 0f;
            OffsetY = slideDistance;
        }

        public void SnapVisible()
        {
            _mode = UiTransitionMode.None;
            _elapsed = 0f;
            Alpha = 1f;
            OffsetY = 0f;
        }

        public void SnapHidden()
        {
            _mode = UiTransitionMode.None;
            _elapsed = 0f;
            Alpha = 0f;
            OffsetY = 0f;
        }

        public bool IsAnimating => _mode != UiTransitionMode.None;

        public void Update(float dt)
        {
            if (_mode == UiTransitionMode.None)
            {
                return;
            }

            _elapsed += dt;
            float t = Math.Clamp(_elapsed / _duration, 0f, 1f);
            float eased = Tween.EaseOut(t);

            switch (_mode)
            {
                case UiTransitionMode.FadeIn:
                    Alpha = eased;
                    OffsetY = 0f;
                    break;
                case UiTransitionMode.FadeOut:
                    Alpha = 1f - eased;
                    OffsetY = 0f;
                    break;
                case UiTransitionMode.FadeInSlideUp:
                    Alpha = eased;
                    OffsetY = _slideDistance * (1f - eased);
                    break;
            }

            if (t >= 1f)
            {
                if (_mode == UiTransitionMode.FadeOut)
                {
                    Alpha = 0f;
                }
                else
                {
                    Alpha = 1f;
                    OffsetY = 0f;
                }

                _mode = UiTransitionMode.None;
            }
        }
    }

    public class UiTransitionController
    {
        private GameState _currentState;
        private GameState? _pendingState;
        private readonly UiTransition _screenTransition = new UiTransition();
        private readonly UiTransition _pauseTransition = new UiTransition();

        public UiTransition ScreenTransition => _screenTransition;
        public UiTransition PauseTransition => _pauseTransition;

        public bool IsTransitioning => _pendingState != null || !_screenTransition.IsComplete;

        public void Initialize(GameState initialState)
        {
            _currentState = initialState;
            _pendingState = null;
            _screenTransition.SnapVisible();
            _pauseTransition.SnapVisible();
        }

        public void RequestStateChange(GameState newState, float fadeDuration = 0.25f)
        {
            if (newState == _currentState && _pendingState == null)
            {
                return;
            }

            _pendingState = newState;
            _screenTransition.BeginFadeOut(fadeDuration);
        }

        public GameState GetDrawState()
        {
            return _currentState;
        }

        public bool TryCompleteTransition()
        {
            if (_pendingState == null)
            {
                return false;
            }

            if (!_screenTransition.IsComplete)
            {
                return false;
            }

            _currentState = _pendingState.Value;
            _pendingState = null;
            _screenTransition.BeginFadeIn(0.25f);
            return true;
        }

        public void OnPauseOpened()
        {
            _pauseTransition.BeginFadeInSlideUp(0.2f, 12f);
        }

        public void OnPauseClosed()
        {
            _pauseTransition.SnapVisible();
        }

        public void Update(float dt)
        {
            _screenTransition.Update(dt);
            _pauseTransition.Update(dt);

            if (_pendingState != null && _screenTransition.IsComplete)
            {
                _currentState = _pendingState.Value;
                _pendingState = null;
                _screenTransition.BeginFadeIn(0.25f);
            }
        }
    }
}
