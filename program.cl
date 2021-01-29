#define GLINTEROP

#ifdef GLINTEROP
__kernel void device_function( write_only image2d_t a, float fov, float3 position, float3 direction, __global float3* p1, __global float3* p2, __global float3* p3, 
	__global float3* t1, __global float3* t2, __global float3* t3, __global float3* color, __global bool* isLight,
	__global float* reflectivity, __global float3* refractionIndex, __global int* texId)
#else
__kernel void device_function( __global int* a, float t )
#endif
{
	// adapted from inigo quilez - iq/2013
	int idx = get_global_id( 0 );
	int idy = get_global_id( 1 );
	int id = idx + 512 * idy;
	if (id >= (512 * 512)) return;
	float2 fragCoord = (float2)( (float)idx, (float)idy ), resolution = (float2)( 512, 512 );
	float3 col = (float3)( 0.f, 0.f, 0.f );
	
}
