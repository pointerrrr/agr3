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

bool castShadowRay(float3 lightPos, float3 intersectionPosition, __global float3* p1, __global float3* p2, __global float3* p3, int objAmount )
{
    float distToLight = length(lightPos - intersectionPosition);
    float3 rayDirection = normalize(lightPos - intersectionPosition);
    for (int i = 0; i < objAmount; i++)
    {
        float currentDistance = Intersect(intersectionPosition - rayDirection * Epsilon, rayDirection, p1[i], p2[i], p3[i]);
        if (isnan(currentDistance) == 1 || isinf(currentDistance) == 1)
            continue;
        if (currentDistance < distToLight && currentDistance > 0.0001f)
        {
            ///printf("%f\n", currentDistance);
            return false;
        }
    }
    return true;
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

int closestBoundingVolume(float3 bbMin1, float3 bbMin2, float3 bbMax1, float3 bbMax2, float3 currentPosition, int current)
{
    float distToLeft = min(length(bbMin1 - currentPosition), length(bbMax1 - currentPosition));
    float distToRight = min(length(bbMin2 - currentPosition), length(bbMax2 - currentPosition));
    if (distToLeft <= distToRight)
        return current;
    else
        return current + 1;
}


#ifdef GLINTEROP
__kernel void device_function( write_only image2d_t a, float fov, float3 position, float3 leftUpperCorner, float3 rightUpperCorner, float3 leftLowerCorner, float3 rightLowerCorner, __global float3* p1, __global float3* p2, __global float3* p3, 
	__global float3* t1, __global float3* t2, __global float3* t3, __global float3* normals, int objAmount, __global float3* color, __global bool* isLight,
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

    

    float3 rayLocation[maxDepth] = { 0 };
    float3 rayDirection[maxDepth] = { 0 };

    float percentLeft = 1.f;
    float oldLeft = 0.f;
    float3 horizontal = rightUpperCorner - leftUpperCorner;
    float3 vertical = leftLowerCorner - leftUpperCorner;
    float3 pixelLocation = leftUpperCorner + (horizontal / 512) * idx + (vertical / 512) * idy;
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

        // TODO: currently we do not use the bvh so try to implement this
        // Determine closest object
        // Check if we hit the root bounding volumen
        if (IntersectAABB(bbMin[0], bbMax[0], currentPosition, currentDirection))
        {
            // calculate closest child
            int current = closestBoundingVolume(bbMin[1], bbMin[2], bbMax[1], bbMax[2], currentPosition, 1);
            // states:
            // 1 from child
            // 2 from sibling
            // 3 from parent
            int state = 3;
            bool run = true;
            while (run) 
            {
                switch (state) 
                {
                case 1 :
                    {
                        //Back in root, finished checking
                        if(current == 0)
                        {
                            run = false;
                            break;
                        }
                        // Need to check sibling
                        if (current == closestBoundingVolume(bbMin[current], bbMin[current + 1], bbMax[current], bbMax[current + 1], currentPosition, current))
                        {
                            // determine new current
                            current = current + 1;
                            state = 2;
                        }
                        // Go back to parent
                        else
                        {
                            current = current / 2;
                            state = 1;
                        }
                        break;

                    }
                case 2 :
                    {
                        if (!IntersectAABB(bbMin[current], bbMax[current], currentPosition, currentDirection))
                        {
                            current = current / 2;
                            state = 1;
                        }
                        else if (current >= 512) 
                        {
                            if (vEnd[current] != 0) 
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
                            current = current / 2;
                            state = 1;
                        }
                        else
                        {
                            current = closestBoundingVolume(bbMin[current * 2], bbMin[current * 2 + 1], bbMax[current * 2], bbMax[current * 2 + 1], currentPosition, current * 2);
                            state = 3;
                        }
                        break;
                    }
                case 3 :
                    {
                        if (!IntersectAABB(bbMin[current], bbMax[current], currentPosition, currentDirection)) 
                        {
                            current = current + 1;
                            state = 2;
                        }
                        else if (current >= 512) 
                        {
                            if (vEnd[current] != 0)
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
                            current = current + 1;
                            state = 1;
                        }
                        else
                        {
                            current = closestBoundingVolume(bbMin[current * 2], bbMin[current * 2 + 1], bbMax[current * 2], bbMax[current * 2 + 1], currentPosition, current * 2);
                            state = 3;
                        }
                        break;

                    }
                }
            }
        }


        /*for (int j = 0; j < objAmount; j++)
        {
            currentDistance = Intersect(currentPosition, currentDirection, p1[j], p2[j], p3[j]);
            // CurrentDistance is NaN of Inf so we do not hit the current object therefore we skip
            if (isnan(currentDistance) == 1 || isinf(currentDistance) == 1)
                continue;
            if (currentDistance < bestDistance) {
                bestDistance = currentDistance;
                closestObject = j;

            }
        }*/

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
            if (castShadowRay(lightPos[j], intersectionPosition, p1, p2, p3, objAmount))
            {
                float distance = length(lightPos[j] - intersectionPosition);
                float attenuation = 1.f / (distance * distance);
                float nDotL = dot(currentNormal, normalize(lightPos[j] - intersectionPosition));
                //printf("%f\n", nDotL);
                if (nDotL < 0)
                    continue;
                illumination += nDotL * attenuation * lightCol[j];
                
                //printf("%f\n", attenuation);
                //printf("%f\n", lightCol[j].x);
            }
        }
        
        // *0.9f + 0.1f * color[(int)closestObject];
        

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

   write_imagef(a, (int2)(idx, idy), (float4)(currentColor.x, currentColor.y, currentColor.z, 0.f));
   //write_imagef(a, (int2)(idx, idy), (float4)(0.f, 1.f, 1.f, 0.f));


    

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

