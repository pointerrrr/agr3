using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using static OpenTK.Vector3;

namespace Template
{
    public static class GlobalLib
    {
        public static float Epsilon = 0.00001f;
        public static int MaxRecursion = 10;
        public static int SamplesPerFrame = 5;
        public static int AA = 4;
        public static float FOV = 120;
        public static int Width = 512, Height = 512;

        public static Vector3 ReflectRay(Vector3 rayDirection, Vector3 normal)
        {
            return rayDirection - 2 * Dot(rayDirection, normal) * normal;
        }

        public static int VecToInt(Vector3 vector)
        {
            int R = vector.X > 1 ? 255 : (int)(vector.X * 255);
            int G = vector.Y > 1 ? 255 : (int)(vector.Y * 255);
            int B = vector.Z > 1 ? 255 : (int)(vector.Z * 255);
            return (R << 16) + (G << 8) + B;
        }

        public static (Vector3, Vector3) GetBoundingVolume(List<Primitive> primitives)
        {
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < primitives.Count; i++)
            {
                (var bbMin, var bbMax) = primitives[i].BoundingBox;

                if (bbMin.X < minX)
                    minX = bbMin.X;
                if (bbMax.X > maxX)
                    maxX = bbMax.X;
                if (bbMin.Y < minY)
                    minY = bbMin.Y;
                if (bbMax.Y > maxY)
                    maxY = bbMax.Y;
                if (bbMin.Z < minZ)
                    minZ = bbMin.Z;
                if (bbMax.Z > maxZ)
                    maxZ = bbMax.Z;
            }

            return (new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }

        // adapted from http://www.cs.uu.nl/docs/vakken/gr/2016/slides/lecture6%20-%20boxes.pdf
        public static bool IntersectAABB((Vector3 min, Vector3 max) volume, Ray ray)
        {
            (var min, var max) = volume;
            float tx1 = (min.X - ray.position.X) / ray.direction.X;
            float tx2 = (max.X - ray.position.X) / ray.direction.X;
            float tmin = Math.Min(tx1, tx2);
            float tmax = Math.Max(tx1, tx2);
            float ty1 = (min.Y - ray.position.Y) / ray.direction.Y;
            float ty2 = (max.Y - ray.position.Y) / ray.direction.Y;
            tmin = Math.Max(tmin, Math.Min(ty1, ty2));
            tmax = Math.Min(tmax, Math.Max(ty1, ty2));
            float tz1 = (min.Z - ray.position.Z) / ray.direction.Z;
            float tz2 = (max.Z - ray.position.Z) / ray.direction.Z;
            tmin = Math.Max(tmin, Math.Min(tz1, tz2));
            tmax = Math.Min(tmax, Math.Max(tz1, tz2));
            return tmax >= tmin;
        }
    }

    
}
