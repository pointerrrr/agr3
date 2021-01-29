using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using static OpenTK.Vector3;
using static Template.GlobalLib;

namespace Template
{
    class Pathtracer : Tracer
    {
        public Vector3[,] resultRaw;
        Random[] random;

        public Pathtracer(int numThreads, int height = 512, int width = 512) : base (numThreads, height, width)
        {
            resultRaw = new Vector3[Width, Height];
            random = new Random[numThreads];
            for (int i = 0; i < numThreads; i++)
                random[i] = new Random();
            MakeScene();
        }

        private void MakeScene()
        {
            var texture1 = new Texture("../../assets/checkers.png");
            var texture2 = new Texture("../../assets/globe.jpg");
            var texture3 = new Texture("../../assets/triangle.jpg");

            //var bvh = new BVH(new List<Primitive>());

            //bvh.Primitives.Add(new Sphere(new Vector3(3, -2, -10), 1) { Material = new Material { color = new Vector3(1, 0, 0), Reflectivity = 0f } });
            //bvh.Primitives.Add(new Sphere(new Vector3(-3, -2, -10), 1) { Material = new Material { color = new Vector3(0, 1, 0), Reflectivity = 0f } });
            //bvh.Primitives.Add(new Sphere(new Vector3(0, 0, -10), 1) { Material = new Material { color = new Vector3(0, 0, 1), Reflectivity = 0f } });

            //bvh.Primitives.Add(new Plane(new Vector3(0, -2, -20), new Vector3(0, 1, 0)) { Material = new Material { color = new Vector3(1, 1, 1), Texture = texture1 } });

            //bvh.Primitives.Add(new Sphere(new Vector3(-5, 0, -5), 1) { Material = new Material { color = new Vector3(0f, 0, 0), RefractionIndex = 1.333f } });

            //bvh.Primitives.Add(new Sphere(new Vector3(5, 0, -5), 1) { Material = new Material { color = new Vector3(1, 1, 1), Reflectivity = 0.5f } });

            //bvh.Primitives.Add(new Vertex(new Vector3(-1, 2, -5), new Vector3(1, 2, -5), new Vector3(0, 1, -5)) { Material = new Material { color = new Vector3(1, 0, 0), Texture = texture3 } });
            //bvh.Primitives.Add(new Vertex(new Vector3(-1, 2, 5), new Vector3(1, 2, 5), new Vector3(0, 1, 5)) { Material = new Material { color = new Vector3(1, 0, 0), Texture = texture3 } });

            //bvh.Primitives.Add(new Sphere(new Vector3(0, 0, -20), 5) { Material = new Material { color = new Vector3(1, 1, 1), Texture = texture2 } });
            
            //bvh.Primitives.Add(new Sphere(new Vector3(0, 0, 5), 3) { Material = new Material { Emittance = new Vector3(100, 100, 100), IsLight = true } });
            //bvh.Primitives.Add(new Sphere(new Vector3(0, 0, -30), 3) { Material = new Material { Emittance = new Vector3(50, 100, 50), IsLight = true } });

            //bvh.Construct();

            //Scene.Add(bvh);

        }

        public override void Trace(Surface screen, int threadId, int numthreads)
        {
            int sqr = (int)Math.Sqrt(numthreads);
            int fromX = (threadId % sqr) * Width / sqr;
            int toX = ((threadId % sqr) + 1) * Width / sqr;
            int fromY = (threadId / sqr) * Height / sqr;
            int toY = ((threadId / sqr) + 1) * Height / sqr;
            for (int x = fromX; x < toX; x++)
            {
                for (int y = fromY; y < toY; y++)
                {
                    Vector3 aaResult = new Vector3();
                    float AAsqrt = (float)Math.Sqrt(AA);
                    for (float aax = 0; aax < AAsqrt; aax++)
                    {
                        for (float aay = 0; aay < AAsqrt; aay++)
                        {
                            Ray ray = new Ray();
                            ray.position = Camera.Position;

                            Vector3 horizontal = Camera.Screen.TopRigth - Camera.Screen.TopLeft;
                            Vector3 vertical = Camera.Screen.BottomLeft - Camera.Screen.TopLeft;

                            Vector3 pixelLocation = Camera.Screen.TopLeft + horizontal / Width * (x + aax * (1f / AAsqrt) - 0.5f) + vertical / Height * (y + aay * (1f / AAsqrt) - 0.5f);

                            Matrix4 rotation = Matrix4.CreateRotationX(Camera.XRotation);
                            rotation *= Matrix4.CreateRotationY(Camera.YRotation);
                            Matrix4 translation = Matrix4.CreateTranslation(Camera.Position);

                            pixelLocation = Transform(pixelLocation, rotation);
                            pixelLocation = Transform(pixelLocation, translation);

                            ray.direction = Normalize(pixelLocation - Camera.Position);

                            for (int i = 0; i < SamplesPerFrame; i++)
                            {
                                var a = TraceRay(ray, threadId, 0);
                                aaResult += a;
                            }

                        }   
                    }

                    resultRaw[x, y] += aaResult;
                    result[x, y] = VecToInt(resultRaw[x, y]/(SamplesTaken * AA));
                }
            }
        }

        public void Clear()
        {
            SamplesTaken = 0;
            resultRaw = new Vector3[Width, Height];
        }

        protected override Vector3 TraceRay(Ray ray, int threadId, int recursionDepth = 0)
        {
            if (recursionDepth > MaxRecursion)
                return new Vector3();

            var nearest = new Intersection { length = float.PositiveInfinity };


            foreach (var primitive in Scene)
            {
                var intersection = primitive.Intersect(ray);
                if (intersection != null && intersection.length < nearest.length)
                    nearest = intersection;
            }

            if (nearest.primitive == null)
                return new Vector3();

            if (nearest.primitive.Material.IsLight)
                return nearest.primitive.Material.Emittance;

            if (nearest.primitive.Material.Reflectivity > 0)
            {
                float chance = (float) random[threadId].NextDouble();
                if (chance < nearest.primitive.Material.Reflectivity)
                {
                    var reflectRay = new Ray { direction = ReflectRay(ray.direction, nearest.normal), position = nearest.Position };
                    return TraceRay(reflectRay, threadId, ++recursionDepth) * nearest.primitive.Material.color;
                }
            }

            if(nearest.primitive.Material.RefractionIndex != 0 )
            {
                float refractionCurrentMaterial = 1.00027717f;
                float refractionIndexNextMaterial = nearest.primitive.Material.RefractionIndex;
                Vector3 primitiveNormal = nearest.normal;

                float thetaOne = Math.Min(1, Math.Max(Dot(ray.direction, primitiveNormal), -1));

                if (thetaOne < 0)
                    thetaOne *= -1;
                else
                {
                    primitiveNormal *= -1;
                    float temp = refractionCurrentMaterial;
                    refractionCurrentMaterial = refractionIndexNextMaterial;
                    refractionIndexNextMaterial = temp;
                }

                float snell = refractionCurrentMaterial / refractionIndexNextMaterial;

                float internalReflection = 1 - snell * snell * (1 - thetaOne * thetaOne);

                if (internalReflection < 0)
                    return Reflect(ray, nearest, threadId, recursionDepth);
                else
                    return TraceRay(new Ray() { direction = Normalize(snell * ray.direction + (snell * thetaOne - (float)Math.Sqrt(internalReflection)) * primitiveNormal), position = nearest.Position + ray.direction * 0.002f }, threadId, recursionDepth++);
            }

            var newDirection = DiffuseReflection(nearest.normal, threadId);

            var newRay = new Ray { direction = newDirection, position = nearest.Position + nearest.normal * 0.001f, length = float.PositiveInfinity };

            float p = (float)( 1f / (2f * Math.PI));

            float cos_theta = Dot(newDirection, nearest.normal);

            Vector3 BRDF;

            if (nearest.primitive.Material.Texture != null)
            {
                nearest.primitive.GetTexture(nearest);
                BRDF = nearest.IntersectionColor / (float)Math.PI;
            }
            else
                BRDF = nearest.primitive.Material.color / (float)Math.PI;

            Vector3 incoming = TraceRay(newRay, threadId, ++recursionDepth);

            return (BRDF * incoming * cos_theta / p);
        }

        protected Vector3 Reflect(Ray ray, Intersection intersection, int threadId, int recursionDepth)
        {
            var reflectionRay = Normalize(ReflectRay(ray.direction, intersection.normal));
            Ray reflection = new Ray() { direction = reflectionRay, position = intersection.Position + reflectionRay * 0.0001f };
            return TraceRay(reflection, threadId, ++recursionDepth) * intersection.primitive.Material.Reflectivity;
        }

        // adapted from https://www.gamedev.net/forums/topic/683176-finding-a-random-point-on-a-sphere-with-spread-and-direction/5315747/
        Vector3 DiffuseReflection(Vector3 Normal, int threadId)
        {
            Vector3 b3 = Normalize(Normal);
            Vector3 different = Math.Abs(b3.X) < 0.5f ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);

            Vector3 b1 = Normalize(Cross(b3, different));
            Vector3 b2 = Cross(b1, b3);

            double z = random[threadId].NextDouble();
            double r = Math.Sqrt(1f - z * z);

            double theta = random[threadId].NextDouble() * Math.PI * 2f - Math.PI;

            double x = r * Math.Cos(theta);
            double y = r * Math.Sin(theta);

            return Normalize((float)x * b1 + (float)y * b2 + (float)z * b3);
        }
    }

    public class PathLight
    {
        public Vector3 Color;
        public Vector3 Position;
        public float Radius;

        public PathLight(Vector3 position, Vector3 color, float radius = 1f)
        {
            Position = position;
            Color = color;
            Radius = radius;
        }

    }
}
