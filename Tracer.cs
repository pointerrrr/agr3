using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using static Template.GlobalLib;
using static OpenTK.Vector3;
using System.IO;

namespace Template
{
    public abstract class Tracer
    {
        public Camera Camera;
        public List<Vertex> Scene = new List<Vertex>();
        public List<Light> Lights = new List<Light>();
        public Skybox Skydome;
        public int[,] result;
        public int Height, Width;
        public float AspectRatio;
        public int SamplesTaken;

        public Tracer(int numThreads, int height = 512, int width = 512)
        {
            Height = height;
            Width = width;
            AspectRatio = width / ((float)height);
            Camera = new Camera(new Vector3(), new Vector3(0, 0, -1), AspectRatio, FOV);
            Skydome = new Skybox("../../assets/skydome.png");
            result = new int[Width, Height];
        }

        public abstract void Trace(Surface screen, int threadId, int numthreads);

        protected abstract Vector3 TraceRay(Ray ray, int threadId, int recursionDepth = 0);

        protected Vector3 reflect(Ray ray, Intersection intersection, int recursionDepth, int threadId)
        {
            var reflectionRay = Normalize(ReflectRay(ray.direction, intersection.normal));
            Ray reflection = new Ray() { direction = reflectionRay, position = intersection.Position + reflectionRay * 0.0001f };
            return TraceRay(reflection, threadId, ++recursionDepth) * intersection.primitive.Material.Reflectivity;
        }

        public List<Vertex> ReadObj(string path, Matrix4 transformation, Texture texture = null)
        {
            var vertices = new List<Vector3>();
            var textures = new List<Vector3>();
            var triangles = new List<Vertex>();
            using (StreamReader streamReader = new StreamReader(path))
            {
                string line;
                

                while((line = streamReader.ReadLine()) != null)
                {
                    line = line.Trim();
                    line = line.Replace("  ", " ");
                    var par = line.Split(' ');

                    switch (par[0])
                    {
                        case "v":
                            var vertex = new Vector3(float.Parse(par[1]), float.Parse(par[2]), float.Parse(par[3]));
                            vertices.Add(Transform(vertex, transformation));
                            break;
                        case "vt":
                            float a = 0;
                            if (par.Length >= 4)
                                float.TryParse(par[3], out a);

                            var tex = new Vector3(float.Parse(par[1]), float.Parse(par[2]), a);
                            textures.Add(tex);
                            break;
                        case "f":

                            if (par.Length == 4)
                            {

                                int index1 = Math.Abs( int.Parse(par[1].Split('/')[0]) - 1) % vertices.Count;
                                int index2 = Math.Abs(int.Parse(par[2].Split('/')[0]) - 1) % vertices.Count;
                                int index3 = Math.Abs(int.Parse(par[3].Split('/')[0]) - 1) % vertices.Count;
                                int tex1 = int.Parse(par[1].Split('/')[1]) - 1;
                                int tex2 = int.Parse(par[2].Split('/')[1]) - 1;
                                int tex3 = int.Parse(par[3].Split('/')[1]) - 1;

                                if (texture != null)
                                    triangles.Add(new Vertex(vertices[index1], vertices[index2], vertices[index3], textures[tex1], textures[tex2], textures[tex3]) { Material = new Material { color = new Vector3(1, 0, 0), Texture = texture } });
                                else
                                    triangles.Add(new Vertex(vertices[index1], vertices[index2], vertices[index3]) { Material = new Material { color = new Vector3(1, 0, 0) } }); //, textures[tex1], textures[tex2], textures[tex3]); );
                            }
                            else
                            {
                                int index1 = int.Parse(par[1].Split('/')[0]) - 1;
                                int index2 = int.Parse(par[2].Split('/')[0]) - 1;
                                int index3 = int.Parse(par[3].Split('/')[0]) - 1;
                                int index4 = int.Parse(par[4].Split('/')[0]) - 1;
                                int tex1 = int.Parse(par[1].Split('/')[1]) - 1;
                                int tex2 = int.Parse(par[2].Split('/')[1]) - 1;
                                int tex3 = int.Parse(par[3].Split('/')[1]) - 1;
                                int tex4 = int.Parse(par[4].Split('/')[1]) - 1;

                                if (texture != null)
                                {
                                    triangles.Add(new Vertex(vertices[index1], vertices[index2], vertices[index3], textures[tex1], textures[tex2], textures[tex3]) { Material = new Material { color = new Vector3(1, 0, 0), Texture = texture } });
                                    triangles.Add(new Vertex(vertices[index3], vertices[index4], vertices[index1], textures[tex3], textures[tex4], textures[tex1]) { Material = new Material { color = new Vector3(1, 0, 0), Texture = texture } });
                                }
                                else
                                {
                                    triangles.Add(new Vertex(vertices[index1], vertices[index2], vertices[index3]) { Material = new Material { color = new Vector3(1, 0, 0) } });
                                    triangles.Add(new Vertex(vertices[index3], vertices[index4], vertices[index1]) { Material = new Material { color = new Vector3(1, 0, 0) } });
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                streamReader.Close();

            }

            return triangles;
        }
    }

    public class Ray
    {
        public float length = float.PositiveInfinity;

        public Vector3 direction;
        public Vector3 position;
        public Vector3 color;
    }

    public class Intersection
    {
        public Ray ray;
        public Primitive primitive;
        public Vector3 normal;
        public Vector3 Position;

        public float length;

        public Vector3 IntersectionColor;

    }
}
