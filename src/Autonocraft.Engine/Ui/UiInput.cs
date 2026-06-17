using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Autonocraft.Engine
{
    public static class UiInput
    {
        public static MouseState ScaleToBackBuffer(MouseState mouse, GameWindow window, GraphicsDevice device)
        {
            var bounds = window.ClientBounds;
            int backBufferWidth = device.PresentationParameters.BackBufferWidth;
            int backBufferHeight = device.PresentationParameters.BackBufferHeight;
            if (bounds.Width <= 0 || bounds.Height <= 0 ||
                (bounds.Width == backBufferWidth && bounds.Height == backBufferHeight))
            {
                return mouse;
            }

            float scaleX = backBufferWidth / (float)bounds.Width;
            float scaleY = backBufferHeight / (float)bounds.Height;
            return new MouseState(
                (int)(mouse.X * scaleX),
                (int)(mouse.Y * scaleY),
                mouse.ScrollWheelValue,
                mouse.LeftButton,
                mouse.MiddleButton,
                mouse.RightButton,
                mouse.XButton1,
                mouse.XButton2);
        }

        public static Point ToClient(Point backBufferPoint, GameWindow window, GraphicsDevice device)
        {
            var bounds = window.ClientBounds;
            int backBufferWidth = device.PresentationParameters.BackBufferWidth;
            int backBufferHeight = device.PresentationParameters.BackBufferHeight;
            if (bounds.Width <= 0 || bounds.Height <= 0 ||
                (bounds.Width == backBufferWidth && bounds.Height == backBufferHeight))
            {
                return backBufferPoint;
            }

            float scaleX = bounds.Width / (float)backBufferWidth;
            float scaleY = bounds.Height / (float)backBufferHeight;
            return new Point(
                (int)(backBufferPoint.X * scaleX),
                (int)(backBufferPoint.Y * scaleY));
        }
    }
}
