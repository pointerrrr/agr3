﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Cloo;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using OpenTK.Input;

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
		OpenCLBuffer<float3> p1, p2, p3, t1, t2, t3, normals, color, lPos, lCol, bbMin, bbMax;
		OpenCLBuffer<float> reflectivity, refractionIndex;
		OpenCLBuffer<int> texId, objAmount, lightAmount, vertexStart, vertexEnd;

		public Surface screen;
		Stopwatch timer = new Stopwatch();
		float t = 21.5f;
		float fov = 90;
		int sceneCount = 0, lightCount = 0;
		Vector3 startPos, TopLeft, TopRigth, BottomLeft, BottomRight;
		// TODO: I dont think direction is needed here, as we need to calculate that for every pixel on the gpu.
		// I think its better to send the corners of the screen towards to gpu instead of the direction
		Vector3 direction;
		Raytracer tracer;
		private KeyboardState prevKeyState, currentKeyState;
		public void Init()
		{
			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
			tracer = new Raytracer(1);
			
			var camera = tracer.Camera;
			Matrix4 rotation = Matrix4.CreateRotationX(camera.XRotation);
			rotation *= Matrix4.CreateRotationY(camera.YRotation);
			Matrix4 translation = Matrix4.CreateTranslation(camera.Position);

			kernel.SetArgument(1, camera.FOV);
			kernel.SetArgument(2, VecToF3(camera.Position));

			kernel.SetArgument(3, VecToF3(Vector3.Transform(camera.Screen.TopLeft, rotation * translation)));
			kernel.SetArgument(4, VecToF3(Vector3.Transform(camera.Screen.TopRigth, rotation * translation)));
			kernel.SetArgument(5, VecToF3(Vector3.Transform(camera.Screen.BottomLeft, rotation * translation)));
			kernel.SetArgument(6, VecToF3(Vector3.Transform(camera.Screen.BottomRight, rotation * translation)));
			int vCount = tracer.BVHs[0].Primitives.Count, lCount = tracer.Lights.Count;
			float3[] p1t = new float3[vCount], p2t = new float3[vCount], p3t = new float3[vCount],
				t1t = new float3[vCount], t2t = new float3[vCount], t3t = new float3[vCount], normal = new float3[vCount], colort = new float3[vCount],
				lPost = new float3[lCount], lColt = new float3[lCount];
			bool[] isLightt = new bool[vCount];
			float[] reflectivityt = new float[vCount], refractionIndext = new float[vCount];
			int[] texIdt = new int[vCount];
			var scene = tracer.Scene;
			int[] vertexStartt = new int[1023];
			int[] vertexEndt = new int[1023];

			float3[] bbMint = new float3[1023], bbMaxt = new float3[1023];

			var lights = tracer.Lights;
			for (int i = 0; i < lights.Count; i++)
			{
				lPost[i] = VecToF3(lights[i].Position);
				lColt[i] = VecToF3(lights[i].Color);
			}

			var queue = new Queue<BVH>();
			queue.Enqueue(tracer.BVHs[0]);
			int index = 0;
			int vertexesSeen = 0;
			while (queue.Count > 0)
			{
				var bvh = queue.Dequeue();
				bbMint[index] = VecToF3(bvh.BoundingBox.Item1);
				bbMaxt[index] = VecToF3(bvh.BoundingBox.Item2);
				if (bvh.IsLeafNode)
				{
					vertexStartt[index] = vertexesSeen;
					foreach (var prim in bvh.Primitives)
					{
						var vertex = prim as Vertex;
						p1t[vertexesSeen] = VecToF3(vertex.Point1);
						p2t[vertexesSeen] = VecToF3(vertex.Point2);
						p3t[vertexesSeen] = VecToF3(vertex.Point3);
						t1t[vertexesSeen] = VecToF3(vertex.Tex1);
						t2t[vertexesSeen] = VecToF3(vertex.Tex2);
						t3t[vertexesSeen] = VecToF3(vertex.Tex3);
						normal[vertexesSeen] = VecToF3(vertex.Normal);
						colort[vertexesSeen] = VecToF3(vertex.Material.color);
						reflectivityt[vertexesSeen] = vertex.Material.Reflectivity;
						refractionIndext[vertexesSeen] = vertex.Material.RefractionIndex;
						texIdt[vertexesSeen] = -1;
						vertexesSeen++;
					}
					vertexEndt[index] = vertexesSeen - vertexStartt[index];
				}
				index++;
				if (bvh.Left != null)
					queue.Enqueue(bvh.Left);
				if (bvh.Right != null)
					queue.Enqueue(bvh.Right);
			}
			
			p1 = new OpenCLBuffer<float3>(ocl, p1t);
			p2 = new OpenCLBuffer<float3>(ocl, p2t);
			p3 = new OpenCLBuffer<float3>(ocl, p3t);
			t1 = new OpenCLBuffer<float3>(ocl, t1t);
			t2 = new OpenCLBuffer<float3>(ocl, t2t);
			t3 = new OpenCLBuffer<float3>(ocl, t3t);
			normals = new OpenCLBuffer<float3>(ocl, normal);
			color = new OpenCLBuffer<float3>(ocl, colort);
			reflectivity = new OpenCLBuffer<float>(ocl, reflectivityt);
			refractionIndex = new OpenCLBuffer<float>(ocl, refractionIndext);
			texId = new OpenCLBuffer<int>(ocl, texIdt);
			lPos = new OpenCLBuffer<float3>(ocl, lPost);
			lCol = new OpenCLBuffer<float3>(ocl, lColt);
			bbMin = new OpenCLBuffer<float3>(ocl, bbMint);
			bbMax = new OpenCLBuffer<float3>(ocl, bbMaxt);
			vertexStart = new OpenCLBuffer<int>(ocl, vertexStartt);
			vertexEnd = new OpenCLBuffer<int>(ocl, vertexEndt);

			kernel.SetArgument(7, p1);
			kernel.SetArgument(8, p2);
			kernel.SetArgument(9, p3);
			kernel.SetArgument(10, t1);
			kernel.SetArgument(11, t2);
			kernel.SetArgument(12, t3);
			kernel.SetArgument(13, normals);
			sceneCount = scene.Count;
			kernel.SetArgument(14, sceneCount);
			kernel.SetArgument(15, color);
			kernel.SetArgument(16, reflectivity);
			kernel.SetArgument(17, refractionIndex);
			kernel.SetArgument(18, texId);
			kernel.SetArgument(19, lPos);
			kernel.SetArgument(20, lCol);
			lightCount = lights.Count;
			kernel.SetArgument(21, lightCount);
			kernel.SetArgument(22, bbMin);
			kernel.SetArgument(23, bbMax);
			kernel.SetArgument(24, vertexStart);
			kernel.SetArgument(25, vertexEnd);
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

		public void Controls(KeyboardState key)
		{
			float movementDistance = 0.25f;
			var camera = tracer.Camera;
			currentKeyState = key;
			bool keyPressed = false;
			if (currentKeyState[Key.W])
			{
				camera.Reposition(new Vector3(0, 0, -movementDistance));
				keyPressed = true;
			}
			if (currentKeyState[Key.A])
			{
				camera.Reposition(new Vector3(-movementDistance, 0, 0));
				keyPressed = true;
			}
			if (currentKeyState[Key.S])
			{
				camera.Reposition(new Vector3(0, 0, movementDistance));
				keyPressed = true;
			}
			if (currentKeyState[Key.D])
			{
				camera.Reposition(new Vector3(movementDistance, 0, 0));
				keyPressed = true;
			}
			if (currentKeyState[Key.E])
			{
				camera.Reposition(new Vector3(0, movementDistance, 0));
				keyPressed = true;
			}
			if (currentKeyState[Key.Q])
			{
				camera.Reposition(new Vector3(0, -movementDistance, 0));
				keyPressed = true;
			}

			if (currentKeyState[Key.Left])
			{
				camera.YRotation += (float)Math.PI / 36f;
				keyPressed = true;
			}
			if (currentKeyState[Key.Right])
			{
				camera.YRotation -= (float)Math.PI / 36f;
				keyPressed = true;
			}
			if (currentKeyState[Key.Up])
			{
				camera.XRotation = (float)(camera.XRotation + Math.PI / 36f > Math.PI / 2f ? Math.PI / 2f : camera.XRotation + Math.PI / 36f);
				keyPressed = true;
			}
			if (currentKeyState[Key.Down])
			{
				camera.XRotation = (float)(camera.XRotation + Math.PI / 36f < -Math.PI / 2f ? -Math.PI / 2f : camera.XRotation - Math.PI / 36f);
				keyPressed = true;
			}

			if (currentKeyState[Key.BracketLeft])
			{
				camera.FOV = tracer.Camera.FOV + 5 > 160 ? 160 : camera.FOV + 5;
				camera.UpdateScreen();
				keyPressed = true;
			}
			if (currentKeyState[Key.BracketRight])
			{
				camera.FOV = tracer.Camera.FOV - 5 < 20 ? 20 : camera.FOV - 5;
				camera.UpdateScreen();
				keyPressed = true;
			}
			prevKeyState = key;

			if(keyPressed)
            {
				Matrix4 rotation = Matrix4.CreateRotationX(camera.XRotation);
				rotation *= Matrix4.CreateRotationY(camera.YRotation);
				Matrix4 translation = Matrix4.CreateTranslation(camera.Position);

				kernel.SetArgument(1, camera.FOV);
				kernel.SetArgument(2, VecToF3(camera.Position));

				kernel.SetArgument(3, VecToF3(Vector3.Transform(camera.Screen.TopLeft, rotation * translation)));
				kernel.SetArgument(4, VecToF3(Vector3.Transform(camera.Screen.TopRigth, rotation * translation)));
				kernel.SetArgument(5, VecToF3(Vector3.Transform(camera.Screen.BottomLeft, rotation * translation)));
				kernel.SetArgument(6, VecToF3(Vector3.Transform(camera.Screen.BottomRight, rotation * translation)));
			}

		}
	}

} // namespace Template