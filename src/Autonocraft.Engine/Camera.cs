using System;
using System.Numerics;

namespace Autonocraft.Engine
{
    public class Camera
    {
        public Vector3 Position { get; set; } = new Vector3(16f, 30f, 16f);
        public float Yaw { get; set; } = -90f;    // facing -Z initially
        public float Pitch { get; set; } = 0f;
        public Vector3 ViewPositionOffset { get; set; }
        public float ViewPitchOffset { get; set; }

        public Vector3 Front
        {
            get
            {
                float yawRad = Yaw * (MathF.PI / 180f);
                float pitchRad = Pitch * (MathF.PI / 180f);

                Vector3 direction;
                direction.X = MathF.Cos(pitchRad) * MathF.Cos(yawRad);
                direction.Y = MathF.Sin(pitchRad);
                direction.Z = MathF.Cos(pitchRad) * MathF.Sin(yawRad);

                return Vector3.Normalize(direction);
            }
        }

        public Vector3 Right => Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
        public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Front));

        public float Roll { get; set; } = 0f;
        public float CurrentFov { get; set; } = 45f;

        public Matrix4x4 GetViewMatrix()
        {
            Vector3 eye = Position + ViewPositionOffset;
            float pitchRad = (Pitch + ViewPitchOffset) * (MathF.PI / 180f);
            float yawRad = Yaw * (MathF.PI / 180f);
            float rollRad = Roll * (MathF.PI / 180f);

            Vector3 front;
            front.X = MathF.Cos(pitchRad) * MathF.Cos(yawRad);
            front.Y = MathF.Sin(pitchRad);
            front.Z = MathF.Cos(pitchRad) * MathF.Sin(yawRad);
            front = Vector3.Normalize(front);

            Vector3 up = Vector3.UnitY;
            if (rollRad != 0f)
            {
                Quaternion q = Quaternion.CreateFromAxisAngle(front, rollRad);
                up = Vector3.Transform(Vector3.UnitY, q);
            }

            return Matrix4x4.CreateLookAt(eye, eye + front, up);
        }

        public Matrix4x4 GetProjectionMatrix(float aspectRatio, float farPlane = 1000f)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(
                CurrentFov * (MathF.PI / 180f),
                aspectRatio,
                0.1f,
                MathF.Max(64f, farPlane));
        }
    }
}
