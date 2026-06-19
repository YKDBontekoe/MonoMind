using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Autonocraft.UI.Menu
{
    /// <summary>
    /// Keyboard and mouse focus helper for vertical menu button rows.
    /// </summary>
    public sealed class MenuFocusList
    {
        private int _focusedIndex;
        private int _hoverIndex = -1;

        public int FocusedIndex => _focusedIndex;
        public int HoverIndex => _hoverIndex;
        public int ActiveIndex => _hoverIndex >= 0 ? _hoverIndex : _focusedIndex;

        public bool IsItemFocused(int index) =>
            index == _focusedIndex || index == _hoverIndex;

        public void Reset(int itemCount)
        {
            _focusedIndex = 0;
            _hoverIndex = -1;
            ClampFocus(itemCount);
        }

        public void Update(
            int itemCount,
            IReadOnlyList<Rectangle> itemRects,
            KeyboardState kb,
            KeyboardState prevKb,
            MouseState mouse,
            float offsetY = 0f)
        {
            if (itemCount <= 0)
            {
                _focusedIndex = 0;
                _hoverIndex = -1;
                return;
            }

            ClampFocus(itemCount);

            _hoverIndex = -1;
            for (int i = 0; i < itemCount && i < itemRects.Count; i++)
            {
                var rect = itemRects[i];
                var hit = new Rectangle(rect.X, rect.Y + (int)offsetY, rect.Width, rect.Height);
                if (hit.Contains(mouse.X, mouse.Y))
                {
                    _hoverIndex = i;
                    _focusedIndex = i;
                    break;
                }
            }

            bool up = kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up);
            bool down = kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down);
            bool tab = kb.IsKeyDown(Keys.Tab) && !prevKb.IsKeyDown(Keys.Tab);

            if (up)
            {
                _focusedIndex = (_focusedIndex + itemCount - 1) % itemCount;
                _hoverIndex = -1;
            }
            else if (down || tab)
            {
                _focusedIndex = (_focusedIndex + 1) % itemCount;
                _hoverIndex = -1;
            }
        }

        public int GetClickedIndex(IReadOnlyList<Rectangle> itemRects, MouseState mouse, MouseState prevMouse, float offsetY = 0f)
        {
            if (mouse.LeftButton != ButtonState.Pressed || prevMouse.LeftButton != ButtonState.Released)
            {
                return -1;
            }

            for (int i = 0; i < itemRects.Count; i++)
            {
                var rect = itemRects[i];
                var hit = new Rectangle(rect.X, rect.Y + (int)offsetY, rect.Width, rect.Height);
                if (hit.Contains(mouse.X, mouse.Y))
                {
                    return i;
                }
            }

            return -1;
        }

        public bool TryConsumeEnter(KeyboardState kb, KeyboardState prevKb, int itemCount, out int index)
        {
            index = -1;
            if (itemCount <= 0)
            {
                return false;
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                index = ActiveIndex;
                return index >= 0 && index < itemCount;
            }

            return false;
        }

        private void ClampFocus(int itemCount)
        {
            if (itemCount <= 0)
            {
                _focusedIndex = 0;
                return;
            }

            _focusedIndex = Math.Clamp(_focusedIndex, 0, itemCount - 1);
        }
    }
}
