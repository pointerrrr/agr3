using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace Template
{
    // skybox for when rays hit nothing
    public class Skybox
    {
        public Texture Texture { get; set; }

        // initialize texture via string
        public Skybox(string path)
        {
            Texture = new Texture(path);
        }
    }

    // texture for all primitives
    public class Texture
    {
        public Vector3[,] Image;

        public Texture(string path)
        {
            Bitmap image = new Bitmap(path);
            Image = new Vector3[image.Width, image.Height];
            for (int i = 0; i < image.Width; i++)
            {
                for (int j = 0; j < image.Height; j++)
                {
                    Color color = image.GetPixel(i, j);
                    Image[i, j] = new Vector3((float)color.R / 255, (float)color.G / 255, (float)color.B / 255);
                }
            }
        }
    }

}
