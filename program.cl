#define GLINTEROP
bool castShadowRay(float3 lightPos, float3 intersectionPosition)
{

}

float Intersect(float3 pos, float3 dir, float3 p1, float3 p2, float3 p3)
{
    float Epsilon = 0.0001f;
    float3 edge1, edge2, h, s, q;
    float a, f, u, v;

    edge1 = p2 - p3;
    edge2 = p3 - p1;
    h = cross(dir, edge2);
    a = dot(edge1, h);
    if (a > -Epsilon && a < Epsilon)
        return 1.f / 0.f;

    f = 1.f / a;

    s = pos - p1;

    u = f * dot(s, h);
    if (u < 0 || u > 1)
        return 1.f / 0.f;

    q = cross(s, edge1);
    v = f * dot(dir, q);
    if (v < 0 || u + v > 1)
        return 1.f / 0.f;
    float t = f * dot(edge2, q);
    if (t < Epsilon)
        return 1.f / 0.f;
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



#ifdef GLINTEROP
__kernel void device_function( write_only image2d_t a, float fov, float3 position, float3 direction, __global float3* p1, __global float3* p2, __global float3* p3, 
	__global float3* t1, __global float3* t2, __global float3* t3, __global float3* normals, int objAmount, __global float3* color, __global bool* isLight,
	__global float* reflectivity, __global float* refractionIndex, __global int* texId, __global float3* lightPos, __global float3* lightCol, int lightAmount)
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
	float3 col = (float3)( 1.f, 2.f, 3.f );
    
    // Loop over every object
    float currentDistance = -1;
    int closestObject = -1;
    float bestDistance = MAXFLOAT;
    // TODO: currently we do not use the bvh so try to implement this
    // Determine closest object
    for (int i = 0; i < objAmount; i++)
    {
        currentDistance = Intersect(position, direction, p1[i], p2[i], p3[i]);

        // CurrentDistance is NaN so we do not hit the current object therefore we continue
        if (isnan(currentDistance) == 0)
            continue;
        if (currentDistance < bestDistance) {
            bestDistance = currentDistance;
            closestObject = i;
        }
    }

    // Check if the normal needs to be flipped
    float3 currentNormal;
    if (dot(normals[closestObject], direction) > 0)
        currentNormal = normals[closestObject] * -1;
    else
        currentNormal = normals[closestObject];


    // TODO: Hit skybox
    if (closestObject == -1)
        return;

    float3 intersectionPosition = position + (direction * bestDistance);
    float3 illumination = (float3)(0.f, 0.f, 0.f);
    for (int i = 0; i < lightAmount; i++)
    {
        if (castShadowRay(lightPos[i], intersectionPosition))
        {
            float distance = length(lightPos[i] - intersectionPosition);
            float attenuation = 1.f / (distance * distance);
            float nDotL = dot(currentNormal, normalize(lightPos[i] - intersectionPosition));

            if (nDotL < 0)
                continue;
            illumination += nDotL * attenuation * lightCol[i];
        }
    }

    if (reflectivity[closestObject] != 0) {
        // TODO: Add reflectivity
    }

    if (refractionIndex[closestObject] != 0)
    {
        // TODO: Add refractions
    }

    if (texId[closestObject] != -1)
    {
        //TODO: Add textures
    }

    // TODO: Add reflectivity, refraction  and texture to color
   float3 currentColor = color[closestObject] * illumination;
    


    write_imagef(a, (int2)(idx, idy), (float4)(0.f, 1.f, 1.f, 0.f));
	
}

/*// Begin steeds meer te denken om dit fucking ding gewoon in de device_fucntion method te gooien
float3 Trace(float3 position, float3 direction, float3* p1, float3* p2, float3* p3, float3* normals, int objAmount, float3* lightPos, float3* lightCol ,int lightAmount)
{
    // Loop over every object
    float currentDistance = -1;
    int closestObject = -1;
    float bestDistance = MAXFLOAT;
    // TODO: currently we do not use the bvh so try to implement this
    // Determine closest object
    for (int i = 0; i < objAmount; i++)
    {
        currentDistance = Intersect(position, direction, p1[i], p2[i], p3[i]);

        // CurrentDistance is NaN so we do not hit the current object therefore we continue
        if (isnan(currentDistance) == 0)
            continue;
        if (currentDistance < bestDistance) {
            bestDistance = currentDistance;
            closestObject = i;
        }
    }

    // Check if the normal needs to be flipped
    float3 currentNormal;
    if (dot(normals[closestObject], direction) > 0)
        currentNormal = normals[closestObject] * -1;
    else
        currentNormal = normals[closestObject];


    // TODO: Hit skybox
    if (closestObject == -1)
        return (float3)(0.f, 0.f, 0.f);

    float3 intersectionPosition = position + (direction * bestDistance);
    float3 illumination = (float3)(0.f, 0.f, 0.f);
    for (int i = 0; i < lightAmount; i++)
    {
        if (castShadowRay(lightPos[i], intersectionPosition))
        {
            float distance = length(lightPos[i] - intersectionPosition);
            float attenuation = 1.f / (distance * distance);
            float nDotL = dot(currentNormal, normalize(lightPos[i] - intersectionPosition));

            if (nDotL < 0)
                continue;
            illumination += nDotL * attenuation * lightCol[i];
        }
    }

    if (reflectivity[closestObject] != 0) {
        // TODO: Add reflectivity
    }

    if (refractionIndex[closestObject] != 0)
    {
        // TODO: Add refractions
    }

    if (texId[closestObject] != -1)
    {
        //TODO: Add textures
    }

    // TODO: Add reflectivity, refraction  and texture to color
    return color[closestObject] * illumination;
    


}*/

