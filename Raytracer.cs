using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using System.Drawing;
using System.Threading;
using static OpenTK.Vector3;
using static Template.GlobalLib;

namespace Template
{
    public class Raytracer : Tracer
    {
        public List<BVH> BVHs = new List<BVH>();
        public Raytracer(int numThreads, int height = 512, int width = 512) : base(numThreads, height, width)
        {
            MakeScene();
        }

        private void MakeScene()
        {
            var objFile1 = "../../assets/capsule.obj";
            var objFile2 = "../../assets/less_basic_box.obj";

            Lights.Add(new Light(new Vector3(0, 0, -1), new Vector3(75, 75, 75)));
            Lights.Add(new Light(new Vector3(1, 6, -1), new Vector3(50, 25, 25)));

            // texture taken from https://mossandfog.com/expand-your-mind-with-these-intricate-fractals/
            var obj1 = ReadObj(objFile1, Matrix4.CreateScale(1f) * Matrix4.CreateRotationY((float)Math.PI * 0.5f) * Matrix4.CreateTranslation(new Vector3(0, -1, -8)), new Texture("../../assets/capsule0.jpg"));
            // texture taken from https://www.clay-and-paint.com/en/texture-plates/30-cernit-texture-plates.html
            var obj2 = ReadObj(objFile2, Matrix4.CreateScale(0.1f) * Matrix4.CreateTranslation(new Vector3(0, -1, 0)), new Texture("../../assets/square.jpg"));



            Scene.AddRange(obj1);
            //Scene.AddRange(obj2);

            //Scene.Add(new Vertex(new Vector3(-3, 3, -8), new Vector3(-3, -3, -8), new Vector3(3, 3, -8)) { Material = new Material { Reflectivity = 1, color = new Vector3(1, 1, 1) } });

            var bvh = new BVH(obj1.Select(x => x as Primitive).ToList());

            bvh.Construct();

            BVHs.Add(bvh);

            //Scene.Add(new Vertex(new Vector3(-3, 3, 8), new Vector3(-3, -3, 8), new Vector3(3, 3, 8)) { Material = new Material { Reflectivity = 0f, color = new Vector3(1, 1, 1) } });
            Scene.Add(new Vertex(new Vector3(3, -3, -1), new Vector3(3, 3, -1), new Vector3(-3, 3, -1)) { Material = new Material { RefractionIndex = 1.3f, color = new Vector3(1, 1, 1) } });
            Scene.Add(new Vertex(new Vector3(3, -3, -1), new Vector3(-3, -3, -1), new Vector3(-3, 3, -1)) { Material = new Material { RefractionIndex = 1.3f, color = new Vector3(1, 1, 1) } });

            //bvh.Primitives.Add(new Sphere(new Vector3(-3, 2, 5), 1.3f) { Material = new Material { color = new Vector3(0.4f, 0.3f, 0.3f), RefractionIndex = 1.453f } });

            //bvh.Construct();

            //Scene.Add(bvh);
        }

        public override void Trace(Surface screen, int threadId, int numthreads)
        {
            for(int pixel = threadId; pixel < Width * Height; pixel += numthreads)
            {
                int x = pixel % Width;
                int y = pixel / Height;
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
                        aaResult += TraceRay(ray, threadId);
                    }
                }
                aaResult /= AA;
                result[x, y] = VecToInt(aaResult);
            }
        }

        protected override Vector3 TraceRay(Ray ray, int threadId, int recursionDepth = 0)
        {
            Vector3 reflectColor = new Vector3();
            Vector3 refractColor = new Vector3();

            if (recursionDepth > 10)
                return new Vector3();
            Intersection nearest = new Intersection { length = float.PositiveInfinity };

            foreach (var primitive in Scene)
            {
                var intersection = primitive.Intersect(ray);
                if (intersection != null && intersection.length < nearest.length)
                    nearest = intersection;
            }

            if (nearest.primitive == null)
                return Skybox(ray);

            var illumination = new Vector3();

            foreach (var light in Lights)
            {

                if (castShadowRay(light, nearest.Position))
                {
                    var distance = (light.Position - nearest.Position).Length;
                    var attenuation = 1f / (distance * distance);
                    var nDotL = Dot(nearest.normal, Normalize(light.Position - nearest.Position));

                    if (nDotL < 0)
                        continue;
                    illumination += nDotL * attenuation * light.Color;
                }

            }
            if (nearest.primitive.Material.Reflectivity != 0)
            {
                reflectColor = reflect(ray, nearest, recursionDepth, threadId);
            }

            if (nearest.primitive.Material.RefractionIndex != 0)
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
                    refractColor = reflect(ray, nearest, recursionDepth, threadId);
                else
                    refractColor = TraceRay(new Ray() { direction = Normalize(snell * ray.direction + (snell * thetaOne - (float)Math.Sqrt(internalReflection)) * primitiveNormal), position = nearest.Position + ray.direction * 0.002f }, threadId, recursionDepth++);
            }

            if (nearest.primitive.Material.Texture != null)
            {
                nearest.primitive.GetTexture(nearest);
                return nearest.IntersectionColor * (1 - nearest.primitive.Material.Reflectivity) * illumination + reflectColor + refractColor; ;
            }
            return nearest.primitive.Material.color * (1 - nearest.primitive.Material.Reflectivity) * illumination + reflectColor + refractColor;
        }

        private bool castShadowRay(Light light, Vector3 position)
        {
            var distToLight = (light.Position - position).Length;
            foreach (var primitive in Scene)
            {
                var intersection = primitive.Intersect(new Ray { position = position, direction = Normalize(light.Position - position) });
                if (intersection != null && intersection.length < distToLight && intersection.length > 0.0001f)
                    return false;
            }
            return true;
        }
        
        private Vector3 Skybox(Ray ray)
        {
            // sphere texturing, adapted from http://www.pauldebevec.com/Probes/
            var direction = -ray.direction;
            float r = (float)(1d / Math.PI * Math.Acos(direction.Z) / Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
            
            float x = r * direction.X + 1;
            float y = r * direction.Y + 1;
            
            int iu = (int)(x * Skydome.Texture.Image.GetLength(0) / 2);
            int iv = (int)(y * Skydome.Texture.Image.GetLength(1) / 2);
            
            if (iu >= Skydome.Texture.Image.GetLength(0) || iu < 0)
                iu = 0;
            if (iv >= Skydome.Texture.Image.GetLength(1) || iv < 0)
                iv = 0;
            return Skydome.Texture.Image[iu, iv];
        }
    }

    public class Light
    {
        public Vector3 Color;
        public Vector3 Position;

        public Light(Vector3 position, Vector3 color)
        {
            Position = position;
            Color = color;
        }
    }
}
