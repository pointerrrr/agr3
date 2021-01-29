#define GLINTEROP

#ifdef GLINTEROP
__kernel void device_function( write_only image2d_t a, float fov, float3 position, float3 direction, __global float3* p1, __global float3* p2, __global float3* p3, 
	__global float3* t1, __global float3* t2, __global float3* t3, __global float3* color, __global bool* isLight,
	__global float* reflectivity, __global float3* refractionIndex, __global int* texId, __global float3* lightPos, __global float3* lightCol)
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

float Intersect(float3 pos, float3 dir, float length, float3 p1, float3 p2, float3 p3)
{
    float Epsilon = 0.0001f;
    float3 edge1, edge2, h, s, q;
    float a, f, u, v;

    edge1 = p2 - p3;
    edge2 = p3 - p1;
    h = cross(dir, edge2);
    a = dot(edge1, h);
    if (a > -Epsilon && a < Epsilon)
        return 1.f/0.f;

    f = 1.f / a;

    s = pos - p1;

    u = f * dot(s, h);
    if (u < 0 || u > 1)
        return 1.f/0.f;

    q = cross(s, edge1);
    v = f * dot(dir, q);
    if (v < 0 || u + v > 1)
        return 1.f/0.f;
    float t = f * dot(edge2, q);
    if (t < Epsilon)
        return 1.f/0.f;
    return t;
    /*intersection.length = t - Epsilon;
    intersection.Position = ray.position + (intersection.length * ray.direction);
    if (Dot(Normal, ray.direction) > 0)
        intersection.normal = -Normal;
    else
        intersection.normal = Normal;

    intersection.primitive = this;
    intersection.ray = ray;

    return intersection;*/
}
