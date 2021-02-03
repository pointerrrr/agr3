#define GLINTEROP
#define maxDepth 10
#define Epsilon 0.00001f

float Intersect(float3 pos, float3 dir, float3 p1, float3 p2, float3 p3)
{
    float3 edge1, edge2, h, s, q;
    float a, f, u, v;

    edge1 = p2 - p1;
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
    if (v < 0.f || u + v > 1.f)
        return 1.f / 0.f;
    float t = f * dot(edge2, q);
    if (t < Epsilon)
        return 1.f / 0.f;
    return t - Epsilon;
}


bool IntersectAABB(float3 minC, float3 maxC, float3 position, float3 direction)
{
    float tx1 = (minC.x - position.x) / direction.x;
    float tx2 = (maxC.x - position.x) / direction.x;
    float tmin = min(tx1, tx2);
    float tmax = max(tx1, tx2);
    float ty1 = (minC.y - position.y) / direction.y;
    float ty2 = (maxC.y - position.y) / direction.y;
    tmin = max(tmin, min(ty1, ty2));
    tmax = min(tmax, max(ty1, ty2));
    float tz1 = (minC.z - position.z) / direction.z;
    float tz2 = (maxC.z - position.z) / direction.z;
    tmin = max(tmin, min(tz1, tz2));
    tmax = min(tmax, max(tz1, tz2));
    return tmax >= tmin;
}

float3 reflect(float3 rayDirection, float3 normal)
{
    return rayDirection - 2.f * dot(rayDirection, normal) * normal;
}

int closestBoundingVolume(__global float3* bbMin, __global float3* bbMax, float3 currentPosition, int index1, int index2)
{
    float index1Dist = min(length(bbMin[index1] - currentPosition), length(bbMax[index1] - currentPosition));
    float index2Dist = min(length(bbMin[index2] - currentPosition), length(bbMax[index2] - currentPosition));
    if (index1Dist < index2Dist)
        return index1;
    else
        return index2;
}

bool castShadowRay(float3 lightPos, float3 currentPositiona, __global float3* p1, __global float3* p2, __global float3* p3, int objAmount,
    __global float3* bbMin, __global float3* bbMax, __global int* vStart, __global int* vEnd )
{
    float bestDistance = length(lightPos - currentPositiona);
    
    float3 currentDirection = normalize(lightPos - currentPositiona);
    float3 currentPosition = currentPositiona + currentDirection * Epsilon;
    if (IntersectAABB(bbMin[0], bbMax[0], currentPosition, currentDirection))
    {
        bool run = true;
        int current = 0;
        int previous = -1;
        while (run)
        {
            if (current > previous && current >= 511)
            {
                if (IntersectAABB(bbMin[current], bbMax[current], currentPosition, currentDirection))
                {
                    if (vEnd[current] > 0)
                    {
                        //bestDistance = MAXFLOAT;
                        for (int j = vStart[current]; j < vStart[current] + vEnd[current]; j++)
                        {
                            float currentDistance = Intersect(currentPosition, currentDirection, p1[j], p2[j], p3[j]);
                            // CurrentDistance is NaN of Inf so we do not hit the current object therefore we skip
                            if (isnan(currentDistance) == 1 || isinf(currentDistance) == 1)
                                continue;
                            if (currentDistance < bestDistance)
                            {
                                return false;
                            }
                        }
                    }
                }
                previous = current;
                current = (current + 1) / 2 - 1;
            }
            else if (current > previous)
            {
                if (IntersectAABB(bbMin[current], bbMax[current], currentPosition, currentDirection))
                {
                    previous = current;
                    current = closestBoundingVolume(bbMin, bbMax, currentPosition, current * 2 + 1, current * 2 + 2);
                }
                else
                {
                    previous = current;
                    current = (current + 1) / 2 - 1;
                }
            }
            else
            {
                int closest = closestBoundingVolume(bbMin, bbMax, currentPosition, current * 2 + 1, current * 2 + 2);
                if (previous == closest)
                {
                    int temp = current;
                    if ((previous & 1) == 0)
                        current = previous - 1;
                    else
                        current = previous + 1;
                    previous = temp;
                }
                else
                {
                    if (current == 0)
                        run = false;
                    previous = current;
                    current = (current + 1) / 2 - 1;
                }

            }
        }
    }
    return true;
}



#ifdef GLINTEROP
__kernel void device_function( write_only image2d_t a, float fov, float3 position, float3 leftUpperCorner, float3 rightUpperCorner, float3 leftLowerCorner, float3 rightLowerCorner, __global float3* p1, __global float3* p2, __global float3* p3, 
	__global float3* t1, __global float3* t2, __global float3* t3, __global float3* normals, int objAmount, __global float3* color,
	__global float* reflectivity, __global float* refractionIndex, __global int* texId, __global float3* lightPos, __global float3* lightCol, int lightAmount,
    __global float3* bbMin, __global float3* bbMax, __global int* vStart, __global int* vEnd)
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

    float3 rayLocation[maxDepth];
    float3 rayDirection[maxDepth];

    for (int i = 0; i < maxDepth; i++)
    {
        rayLocation[i] = (float3)(0.f);
        rayDirection[i] = (float3)(0.f);
    }

    float percentLeft = 1.f;
    float oldLeft = 0.f;
    float3 horizontal = rightUpperCorner - leftUpperCorner;
    float3 vertical = leftLowerCorner - leftUpperCorner;
    float3 pixelLocation = leftUpperCorner + (horizontal / 512.f) * idx + (vertical / 512.f) * idy;
    int counter = 1;
    float3 reflectionColor = (float3)(0.f, 0.f, 0.f);
    float3 refractionColor = (float3)(0.f, 0.f, 0.f);

    rayLocation[0] = position;
    
    rayDirection[0] = normalize(pixelLocation - position);

    float3 currentColor = (float3)(0.f);

    // Recursion is not allowed so my idea was to use an array or a queue to put the rays in it
    // I started working on an array but a down side is that arrays of flexible size are not allowed. 
    for (int i = 0; i < maxDepth; i++) {
        // no ray present
        if (length(rayDirection[i]) <= 0)
            continue;

        // Get current position and ray direction
        float3 currentPosition = rayLocation[i];
        float3 currentDirection = rayDirection[i];
        float bestDistance = MAXFLOAT;
        // Loop over every object
        float currentDistance = -1;
        int closestObject = -1;


        // currentgetal *2 ( currentgetal * 2) + 1

        // Determine closest object
        // Check if we hit the root bounding volumen
        // bvh-traversal algorithm adapted from: http://www.davidovic.cz/wiki/lib/exe/fetch.php/school/hapala_sccg2011/hapala_sccg2011.pdf
        if (IntersectAABB(bbMin[0], bbMax[0], currentPosition, currentDirection))
        {
            // calculate closest child
            //int current = closestBoundingVolume(bbMin, bbMax, currentPosition, 1, 2);
            // states:
            // 1 from child
            // 2 from sibling
            // 3 from parent
            int state = 3;
            bool run = true;
            int counter = 0;
            int current = 0;
            int previous = -1;
            while (run && counter < 1024)
            {
                //if (idx == 229 && idy == 283)
                    //printf("%f, %f\n", (float)current, (float)previous);
                counter++;
                if (current > previous && current >= 511)
                {
                    if (IntersectAABB(bbMin[current], bbMax[current], currentPosition, currentDirection))
                    {
                        if (vEnd[current] > 0)
                        {
                            bestDistance = MAXFLOAT;
                            for (int j = vStart[current]; j < vStart[current] + vEnd[current]; j++)
                            {
                                currentDistance = Intersect(currentPosition, currentDirection, p1[j], p2[j], p3[j]);
                                // CurrentDistance is NaN of Inf so we do not hit the current object therefore we skip
                                if (isnan(currentDistance) == 1 || isinf(currentDistance) == 1)
                                    continue;
                                if (currentDistance < bestDistance) {
                                    bestDistance = currentDistance;
                                    closestObject = j;

                                }
                            }
                            if (closestObject != -1)
                                run = false;
                        }
                    }
                    previous = current;
                    current = (current + 1) / 2 - 1;
                }
                else if (current > previous)
                {
                    if (IntersectAABB(bbMin[current], bbMax[current], currentPosition, currentDirection))
                    {
                        previous = current;
                        current = closestBoundingVolume(bbMin, bbMax, currentPosition, current * 2 + 1, current * 2 + 2);
                    }
                    else
                    {
                        previous = current;
                        current = (current + 1) / 2 - 1;
                    }
                }
                else
                {
                    int closest = closestBoundingVolume(bbMin, bbMax, currentPosition, current * 2 + 1, current * 2 + 2);
                    if (previous == closest)
                    {
                        int temp = current;
                        if ((previous & 1) == 0)
                            current = previous - 1;                        
                        else
                            current = previous + 1;
                        previous = temp;                        
                    }
                    else
                    {
                        if (current == 0)
                            run = false;
                        previous = current;
                        current = (current + 1) / 2 - 1;
                    }

                }
            }
        }
       

        // TODO: Hit skybox
        if (closestObject < 0)
            continue;

        //currentColor = (float3)(1.f/bestDistance);
        //break;
        // Check if the normal needs to be flipped
        float3 currentNormal;
        if (dot(normals[(int)closestObject], currentDirection) > 0)
            currentNormal = normals[(int)closestObject] * -1;
        else
            currentNormal = normals[(int)closestObject];

        float3 intersectionPosition = currentPosition - currentDirection * Epsilon + (currentDirection * bestDistance);
        float3 illumination = (float3)(0.f, 0.f, 0.f);
        for (int j = 0; j < lightAmount; j++)
        {
            if (castShadowRay(lightPos[j], intersectionPosition, p1, p2, p3, objAmount, bbMin, bbMax, vStart, vEnd))
            {
                float distance = length(lightPos[j] - intersectionPosition);
                float attenuation = 1.f / (distance * distance);
                float nDotL = dot(currentNormal, normalize(lightPos[j] - intersectionPosition));
                if (nDotL < 0)
                    continue;
                illumination += nDotL * attenuation * lightCol[j];
            }
        }
        
        if (reflectivity[closestObject] > 0)
        {
            if (i + 1 >= maxDepth)
                break;
            rayLocation[i + 1] = intersectionPosition;
            rayDirection[i + 1] = reflect(rayDirection[i], currentNormal);
            oldLeft = percentLeft;
            percentLeft *= reflectivity[closestObject];
            currentColor += (1.f - reflectivity[closestObject]) * color[closestObject] * (oldLeft - percentLeft);
            
        }

        else if (refractionIndex[closestObject] != 0)
        {
            float refractionCurrentMaterial = 1.00027717f;
            float refractionIndexNextMaterial = refractionIndex[closestObject];
            
            float thetaOne = min(1.f, max(dot(currentDirection, currentNormal), -1.f));

            if (thetaOne < 0)
                thetaOne *= -1;
            else
            {
                currentNormal *= -1;
                float temp = refractionCurrentMaterial;
                refractionCurrentMaterial = refractionIndexNextMaterial;
                refractionIndexNextMaterial = temp;
            }

            float snell = refractionCurrentMaterial / refractionIndexNextMaterial;

            float internalReflection = 1 - snell * snell * (1 - thetaOne * thetaOne);

            if (internalReflection < 0)
            {
                if (i + 1 >= maxDepth)
                    break;
                rayLocation[i + 1] = intersectionPosition;
                rayDirection[i + 1] = reflect(currentDirection, currentNormal);

            }
            else
            {
                refractionColor = (float3)(1.f);// TraceRay(new Ray(){ direction = Normalize(snell * ray.direction + (snell * thetaOne - (float)Math.Sqrt(internalReflection)) * currentNormal), position = nearest.Position + ray.direction * 0.002f }, threadId, recursionDepth++);
                if (i + 1 >= maxDepth)
                    break;
                rayLocation[i + 1] = intersectionPosition;
                rayDirection[i + 1] = normalize(snell * currentDirection + (snell * thetaOne - sqrt(internalReflection)) * currentNormal);
            }
        }
        else
        {
            currentColor += color[closestObject] * illumination * percentLeft;
        }

        if (texId[closestObject] != -1)
        {
            //TODO: Add textures
        }
        
    }
   
    
    // TODO: Add reflectivity, refraction  and texture to color
   
    if (currentColor.x > 1)
        currentColor.x = 1;
    if (currentColor.y > 1)
        currentColor.y = 1;
    if (currentColor.z > 1)
        currentColor.z = 1;
    
    //int r = (int)clamp(255.f * currentColor.x, 0.f, 255.f);
    //int g = (int)clamp(255.f * currentColor.y, 0.f, 255.f);
    //int b = (int)clamp(255.f * currentColor.z, 0.f, 255.f);
    //a[id] = (r << 16) + (g << 8) + b;
    write_imagef(a, (int2)(idx, idy), (float4)(currentColor.x, currentColor.y, currentColor.z, 0.f));
    ////write_imagef(a, (int2)(idx, idy), (float4)(0.f, 1.f, 1.f, 0.f));
}
