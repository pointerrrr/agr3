using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using static OpenTK.Vector3;
using static Template.GlobalLib;

namespace Template
{
    public abstract class Primitive
    {
        public Material Material;

        public (Vector3, Vector3) BoundingBox;

        public Vector3 Centroid;

        public abstract Intersection Intersect(Ray ray);
        public abstract void GetTexture(Intersection intersection);

        
    }

    public class Sphere : Primitive
    {
        public Vector3 Position;
        public float Radius;
        public float Radius2;

        public Sphere(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
            Radius2 = radius * radius;

            BoundingBox = (new Vector3(Position.X - radius, Position.Y - radius, Position.Z - radius),
                new Vector3(Position.X + radius, Position.Y + radius, Position.Z + radius));

            Centroid = Position;
        }

        public override Intersection Intersect(Ray ray)
        {
            // efficient ray / sphere intersection adapted from the lecture slides
            Vector3 C = Position - ray.position;

            float t = Dot(C, ray.direction);
            Vector3 Q = C - t * ray.direction;
            float p2 = Dot(Q, Q);

            if (p2 > Radius2)
                return null;

            var intersection = new Intersection();

            intersection.primitive = this;
            intersection.ray = ray;


            if (C.Length < Radius)
            {
                t += (float)Math.Sqrt(Radius2 - p2);

                intersection.length = t - 0.0001f;
                intersection.Position = intersection.length * ray.direction + ray.position;
                intersection.normal = Normalize(intersection.Position - Position);

                return intersection;
            }

            t -= (float)Math.Sqrt(Radius2 - p2);

            if (((t < ray.length) && (t > 0)))
            {

                intersection.length = t - 0.0001f;
                intersection.Position = intersection.length * ray.direction + ray.position;
                intersection.normal = Normalize(intersection.Position - Position);
                
                return intersection;
            }
            return null;            
        }

        public override void GetTexture(Intersection intersection)
        {
            // sphere texturing, adapted from http://www.pauldebevec.com/Probes/
            var direction = Normalize(Position - intersection.Position);
            float r = (float)(1d / Math.PI * Math.Acos(direction.Z) / Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
            // finding the coordinates
            float x = r * direction.X + 1;
            float y = r * direction.Y + 1;
            // scaling the coordinates to image size
            int iu = (int)(x * Material.Texture.Image.GetLength(0) / 2);
            int iv = (int)(y * Material.Texture.Image.GetLength(1) / 2);
            // fail-safe to make sure the returned value is always within the image
            if (iu >= Material.Texture.Image.GetLength(0) || iu < 0)
                iu = 0;
            if (iv >= Material.Texture.Image.GetLength(1) || iv < 0)
                iv = 0;
            intersection.IntersectionColor = Material.Texture.Image[iu, iv];
        }
    }

    public class Plane : Primitive
    {
        public Vector3 Position;
        public Vector3 Normal;

        public Plane(Vector3 position, Vector3 normal)
        {
            Position = position;
            Normal = normal;

            BoundingBox = (new Vector3(float.PositiveInfinity), new Vector3(float.NegativeInfinity));
        }

        public override Intersection Intersect(Ray ray)
        {
            float par = Dot(ray.direction, Normal);
            float t = (Dot(Position - ray.position, Normal))  / par;

            if (Math.Abs(par) < 0.0001f || t < 0)
                return null;

            var intersection = new Intersection();

            intersection.length = t - 0.0001f;
            intersection.primitive = this;
            intersection.ray = ray;
            intersection.normal = par > 0 ? -Normal : Normal;
            intersection.Position = ray.position + ray.direction * intersection.length + Normal * 0.001f;

            return intersection;
        }

        public override void GetTexture(Intersection intersection)
        {
            // tilt the plane for easy texture coordinate calculation
            Vector3 temp = intersection.Position - intersection.normal * intersection.Position.Y;
            float x, y;
            x = (temp.X + 10000) % 1;
            y = (temp.Z + 10000) % 1;
            if (x >= 1 || x < 0)
                x = 0;
            if (y >= 1 || y < 0)
                y = 0;

            intersection.IntersectionColor = Material.Texture.Image[(int)(Material.Texture.Image.GetLength(0) * x), (int)(Material.Texture.Image.GetLength(1) * y)];
        }
    }

    // adapted from https://en.wikipedia.org/wiki/M%C3%B6ller%E2%80%93Trumbore_intersection_algorithm
    public class Vertex : Primitive
    {
        public Vector3 Point1, Point2, Point3, Normal, Tex1 = new Vector3(0, 0, 0), Tex2 = new Vector3 (0.5f, 1, 0), Tex3 = new Vector3(1, 0, 0);

        public Vertex(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Point1 = p1;
            Point2 = p2;
            Point3 = p3;

            Normal = Normalize( Cross(p2 - p1, p3 - p1));

            var minBB = new Vector3(Math.Min(Math.Min(p1.X, p2.X), p3.X), Math.Min(Math.Min(p1.Y, p2.Y), p3.Y), Math.Min(Math.Min(p1.Z, p2.Z), p3.Z));
            var maxBB = new Vector3(Math.Max(Math.Max(p1.X, p2.X), p3.X), Math.Max(Math.Max(p1.Y, p2.Y), p3.Y), Math.Max(Math.Max(p1.Z, p2.Z), p3.Z));

            BoundingBox = (minBB, maxBB);

            Centroid = new Vector3((p1.X + p2.X + p3.X) / 3f, (p1.Y + p2.Y + p3.Y) / 3f, (p1.Z + p2.Z + p3.Z) / 3f);
        }

        public Vertex(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 t1, Vector3 t2, Vector3 t3)
        {
            Point1 = p1;
            Point2 = p2;
            Point3 = p3;

            Normal = Normalize(Cross(p2 - p1, p3 - p1));

            Tex1 = t1;
            Tex2 = t2;
            Tex3 = t3;

            var minBB = new Vector3(Math.Min(Math.Min(p1.X, p2.X), p3.X), Math.Min(Math.Min(p1.Y, p2.Y), p3.Y), Math.Min(Math.Min(p1.Z, p2.Z), p3.Z));
            var maxBB = new Vector3(Math.Max(Math.Max(p1.X, p2.X), p3.X), Math.Max(Math.Max(p1.Y, p2.Y), p3.Y), Math.Max(Math.Max(p1.Z, p2.Z), p3.Z));

            BoundingBox = (minBB, maxBB);

            Centroid = new Vector3((p1.X + p2.X + p3.X) / 3f, (p1.Y + p2.Y + p3.Y) / 3f, (p1.Z + p2.Z + p3.Z) / 3f);
        }

        public override Intersection Intersect(Ray ray)
        {
            Vector3 edge1, edge2, h, s, q;
            float a, f, u, v;

            edge1 = Point2 - Point1;
            edge2 = Point3 - Point1;
            h = Cross(ray.direction, edge2);
            a = Dot(edge1, h);
            if (a > -Epsilon && a < Epsilon)
                return null;

            f = 1f / a;

            s = ray.position - Point1;

            u = f * Dot(s, h);
            if (u < 0 || u > 1)
                return null;

            q = Cross(s, edge1);
            v = f * Dot(ray.direction, q);
            if (v < 0 || u + v > 1)
                return null;
            float t = f * Dot(edge2, q);
            if (t < Epsilon)
                return null;
            var intersection = new Intersection();
            intersection.length = t - Epsilon;
            intersection.Position = ray.position + (intersection.length * ray.direction);
            if (Dot(Normal, ray.direction) > 0)
                intersection.normal = -Normal;
            else
                intersection.normal = Normal;
            
            intersection.primitive = this;
            intersection.ray = ray;

            return intersection;
        }

        public override void GetTexture(Intersection intersection)
        {
            var barycentric = getBarycentricCoordinatesAt(intersection.Position);

            // adapted from https://computergraphics.stackexchange.com/questions/1866/how-to-map-square-texture-to-triangle

            var texturelocation = barycentric.X * Tex1 + barycentric.Y * Tex2 + barycentric.Z * Tex3;

            float x = Math.Abs(texturelocation.X % 1);
            float y = Math.Abs(texturelocation.Y % 1);

            intersection.IntersectionColor = Material.Texture.Image[(int)(Material.Texture.Image.GetLength(0) * x), (int)(Material.Texture.Image.GetLength(1) * y)];
        }

        // adapted from https://gamedev.stackexchange.com/questions/23743/whats-the-most-efficient-way-to-find-barycentric-coordinates
        private Vector3 getBarycentricCoordinatesAt( Vector3 pos )
        {
            Vector3 bary = new Vector3();

            // The area of a triangle is 
            float areaABC = Dot(Normal, Cross(Point2 - Point1, Point3 - Point1));
            float areaPBC = Dot(Normal, Cross(Point2 - pos, Point3 - pos));
            float areaPCA = Dot(Normal, Cross(Point3 - pos, Point1 - pos));

            bary.X = areaPBC / areaABC ; // alpha
            bary.Y = areaPCA / areaABC ; // beta
            bary.Z = 1.0f - bary.X - bary.Y ; // gamma

            return bary;
        }
    }

    public class Material
    {
        public Vector3 color;
        public Vector3 Emittance;
        public bool IsLight;
        public float Reflectivity;
        public float RefractionIndex;

        public Texture Texture = null;
    }
}
