using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using static OpenTK.Vector3;

namespace Template
{
    public class Camera
    {
        public Vector3 Position;
        public Vector3 Direction;
        public Screen Screen;

        public float FOV;
        private float screenDistance;
        public float FocalDistance;
        public float ApertureSize;
        public float YRotation, XRotation;
        public float AspectRatio;

        public Camera(Vector3 position, Vector3 direction, float aspectRatio, float fov = 120)
        {
            Position = position;
            Direction = direction;
            FOV = fov;
            AspectRatio = aspectRatio;
            UpdateScreen();
        }

        public void Reposition(Vector3 vector)
        {
            Matrix4 rotation = Matrix4.CreateRotationX(XRotation);
            rotation *= Matrix4.CreateRotationY(YRotation);
            vector = Transform(vector, rotation);
            Position += vector;
        }

        public void UpdateScreen()
        {
            screenDistance = 1 / (float)Math.Tan(FOV * (Math.PI / 180) / 2);
            var leftTop = new Vector3(-AspectRatio, 1, -screenDistance);
            var rightTop = new Vector3(AspectRatio, 1, -screenDistance) ;
            var leftBottom = new Vector3(-AspectRatio, -1, -screenDistance);
            var rightBottom = new Vector3(AspectRatio, -1, -screenDistance);
            Screen = new Screen(leftTop, rightTop, leftBottom, rightBottom);
        }

    }

    public class Screen
    {
        public Vector3 TopLeft, TopRigth, BottomLeft, BottomRight;
        
        public Screen(Vector3 topleft, Vector3 topright, Vector3 bottomleft, Vector3 bottomright)
        {
            TopLeft = topleft;
            TopRigth = topright;
            BottomLeft = bottomleft;
            BottomRight = bottomright;
        }
    }
}
