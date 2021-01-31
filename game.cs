using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Cloo;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK;

namespace Template {

	class Game
	{
		// when GLInterop is set to true, the fractal is rendered directly to an OpenGL texture
		bool GLInterop = true;
		// load the OpenCL program; this creates the OpenCL context
		static OpenCLProgram ocl = new OpenCLProgram( "../../program.cl" );
		// find the kernel named 'device_function' in the program
		OpenCLKernel kernel = new OpenCLKernel( ocl, "device_function" );
		// create a regular buffer; by default this resides on both the host and the device
		OpenCLBuffer<int> buffer = new OpenCLBuffer<int>( ocl, 512 * 512 );
		// create an OpenGL texture to which OpenCL can send data
		OpenCLImage<int> image = new OpenCLImage<int>( ocl, 512, 512 );
		OpenCLBuffer<float3> p1, p2, p3, t1, t2, t3, normals, color, lPos, lCol;
		OpenCLBuffer<bool> isLight;
		OpenCLBuffer<float> reflectivity, refractionIndex;
		OpenCLBuffer<int> texId, objAmount, lightAmount;
		public Surface screen;
		Stopwatch timer = new Stopwatch();
		float t = 21.5f;
		float fov = 90;
		Vector3 startPos, TopLeft, TopRigth, BottomLeft, BottomRight;
		// TODO: I dont think direction is needed here, as we need to calculate that for every pixel on the gpu.
		// I think its better to send the corners of the screen towards to gpu instead of the direction
		Vector3 direction;
		public void Init()
		{
			var raytracer = new Raytracer(1);
			int vCount = raytracer.Scene.Count, lCount = raytracer.Lights.Count;
			float3[] p1t = new float3[vCount], p2t = new float3[vCount], p3t = new float3[vCount],
				t1t = new float3[vCount], t2t = new float3[vCount], t3t = new float3[vCount], normal = new float3[vCount],colort = new float3[vCount],
				lPost = new float3[lCount], lColt = new float3[lCount];
			bool[] isLightt = new bool[vCount];
			float[] reflectivityt = new float[vCount], refractionIndext = new float[vCount];
			int[] texIdt = new int[vCount];
			var scene = raytracer.Scene;
			for(int i = 0; i < scene.Count; i++)
            {
				p1t[i] = VecToF3(scene[i].Point1);
				p2t[i] = VecToF3(scene[i].Point2);
				p3t[i] = VecToF3(scene[i].Point3);
				t1t[i] = VecToF3(scene[i].Tex1);
				t2t[i] = VecToF3(scene[i].Tex2);
				t3t[i] = VecToF3(scene[i].Tex3);
				normal[i] = VecToF3(scene[i].Normal);
				colort[i] = VecToF3(scene[i].Material.color);
				reflectivityt[i] = scene[i].Material.Reflectivity;
				refractionIndext[i] = scene[i].Material.RefractionIndex;
				texIdt[i] = -1;
				isLightt[i] = false;
			}
			var lights = raytracer.Lights;
			for(int i = 0; i < lights.Count; i++)
            {
				lPost[i] = VecToF3(lights[i].Position);
				lColt[i] = VecToF3(lights[i].Color);
			}

			p1 = new OpenCLBuffer<float3>(ocl, p1t);
			p2 = new OpenCLBuffer<float3>(ocl, p2t);
			p3 = new OpenCLBuffer<float3>(ocl, p3t);
			t1 = new OpenCLBuffer<float3>(ocl, t1t);
			t2 = new OpenCLBuffer<float3>(ocl, t2t);
			t3 = new OpenCLBuffer<float3>(ocl, t3t);
			normals = new OpenCLBuffer<float3>(ocl, normal);
			objAmount = new OpenCLBuffer<int>(ocl, scene.Count);
			color = new OpenCLBuffer<float3>(ocl, colort);
			isLight = new OpenCLBuffer<bool>(ocl, isLightt);
			reflectivity = new OpenCLBuffer<float>(ocl, reflectivityt);
			refractionIndex = new OpenCLBuffer<float>(ocl, refractionIndext);
			texId = new OpenCLBuffer<int>(ocl, texIdt);
			lPos = new OpenCLBuffer<float3>(ocl, lPost);
			lCol = new OpenCLBuffer<float3>(ocl, lColt);
			lightAmount = new OpenCLBuffer<int>(ocl, lights.Count);

			kernel.SetArgument(4, p1);
			kernel.SetArgument(5, p2);
			kernel.SetArgument(6, p3);
			kernel.SetArgument(7, t1);
			kernel.SetArgument(8, t2);
			kernel.SetArgument(9, t3);
			kernel.SetArgument(10, normals);
			kernel.SetArgument(11, objAmount);
			kernel.SetArgument(12, color);
			kernel.SetArgument(13, isLight);
			kernel.SetArgument(14, reflectivity);
			kernel.SetArgument(15, refractionIndex);
			kernel.SetArgument(16, texId);
			kernel.SetArgument(17, lPos);
			kernel.SetArgument(18, lCol);
			kernel.SetArgument(19, lightAmount);
		}

		float3 VecToF3(Vector3 vec)
        {
			return new float3(vec.X, vec.Y, vec.Z);
        }

		public void Tick()
		{
			GL.Finish();
			// clear the screen
			screen.Clear( 0 );
			// do opencl stuff
			if (GLInterop)
				kernel.SetArgument( 0, image );
			else
				kernel.SetArgument( 0, buffer );

			kernel.SetArgument(1, fov);
			kernel.SetArgument(2, VecToF3(startPos));
			kernel.SetArgument(3, VecToF3(direction));

			t += 0.1f;
 			// execute kernel
			long [] workSize = { 512, 512 };
			long [] localSize = { 32, 4 };
			if (GLInterop)
			{
				// lock the OpenGL texture for use by OpenCL
				kernel.LockOpenGLObject( image.texBuffer );
				// execute the kernel
				kernel.Execute( workSize, localSize );
				// unlock the OpenGL texture so it can be used for drawing a quad
				kernel.UnlockOpenGLObject( image.texBuffer );
			}
			else
			{
				// execute the kernel
				kernel.Execute( workSize, localSize );
				// get the data from the device to the host
				buffer.CopyFromDevice();
				// plot pixels using the data on the host
				for( int y = 0; y < 512; y++ ) for( int x = 0; x < 512; x++ )
				{
					screen.pixels[x + y * screen.width] = buffer[x + y * 512];
				}
			}
		}
		public void Render() 
		{
			// use OpenGL to draw a quad using the texture that was filled by OpenCL
			if (GLInterop)
			{
				GL.LoadIdentity();
				GL.BindTexture( TextureTarget.Texture2D, image.OpenGLTextureID );
				GL.Begin( PrimitiveType.Quads );
				GL.TexCoord2( 0.0f, 1.0f ); GL.Vertex2( -1.0f, -1.0f );
				GL.TexCoord2( 1.0f, 1.0f ); GL.Vertex2(  1.0f, -1.0f );
				GL.TexCoord2( 1.0f, 0.0f ); GL.Vertex2(  1.0f,  1.0f );
				GL.TexCoord2( 0.0f, 0.0f ); GL.Vertex2( -1.0f,  1.0f );
				GL.End();
			}
		}
	}

} // namespace Template