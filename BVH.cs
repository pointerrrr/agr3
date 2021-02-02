using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using static Template.GlobalLib;
using OpenTK;
using static OpenTK.Vector3;

namespace Template
{
    public class BVH : Primitive
    {
        public bool IsLeafNode = false;
        public static int BinCount = 16, MaxSplitDepth = 8;
        public int CurrentSplitDepth = 0;
        public float SplitCost = float.PositiveInfinity;
        public List<Primitive> Primitives;
        public BVH Left, Right;

        public BVH(List<Primitive> primitives)
        {
            Primitives = primitives;
        }

        public override Intersection Intersect(Ray ray)
        {
            var intersection = IntersectAABB(BoundingBox, ray);
            if (intersection)
                return IntersectSubNode(ray);
            else
                return null;
        }

        public Intersection IntersectSubNode(Ray ray)
        {
            if(IsLeafNode)
            {
                var nearest = new Intersection { length = float.PositiveInfinity };

                for(int i = 0; i < Primitives.Count; i++)
                {
                    var intersection = Primitives[i].Intersect(ray);
                    if (intersection != null && intersection.length < nearest.length)
                        nearest = intersection;
                }

                if (nearest.primitive != null)
                    return nearest;
                return null;
            }

            var intersectLeft = IntersectAABB(Left.BoundingBox, ray);
            var intersectRight = IntersectAABB(Right.BoundingBox, ray);

            if (!intersectLeft && !intersectRight )
                return null;

            if (!intersectLeft)
                return Right.IntersectSubNode(ray);

            if (!intersectRight)
                return Left.IntersectSubNode(ray);


            var distToleft = Math.Min((Left.BoundingBox.Item1 - ray.position).Length, (Left.BoundingBox.Item2 - ray.position).Length);
            var distToRigth = Math.Min((Right.BoundingBox.Item1 - ray.position).Length, (Right.BoundingBox.Item2 - ray.position).Length);

            if (distToleft > distToRigth)
            {
                var intersectionRight = Right.IntersectSubNode(ray);

                if (intersectionRight == null)
                    return Left.IntersectSubNode(ray);
                else
                {
                    var left = Left.IntersectSubNode(ray);
                    if (left != null && left.length < intersectionRight.length)
                        return left;
                    return intersectionRight;
                }
            }
            else
            {
                var intersectionLeft = Left.IntersectSubNode(ray);

                if (intersectionLeft == null)
                    return Right.IntersectSubNode(ray);
                else
                {
                    var right = Right.IntersectSubNode(ray);
                    if (right != null && right.length < intersectionLeft.length)
                        return right;
                    return intersectionLeft;
                }
            }
        }

        public void Construct()
        {
            var stopwatch = new Stopwatch();
            Console.WriteLine("Starting BVH construction with " + Primitives.Count + " primitives");
            stopwatch.Start();
            //MaxSplitDepth = (int) Math.Ceiling(Math.Log(Primitives.Count, 2));
            BoundingBox = GetBoundingVolume(Primitives);
            SubDivide();
            stopwatch.Stop();
            Console.WriteLine("Finished BVH construction in " + stopwatch.Elapsed);
        }

        private void SubDivide()
        {
            if(CurrentSplitDepth > MaxSplitDepth)
            {
                IsLeafNode = true;
                return;
            }
            (var bbMin, var bbMax) = BoundingBox;

            var xDist = Math.Abs(bbMin.X - bbMax.X);
            var yDist = Math.Abs(bbMin.Y - bbMax.Y);
            var zDist = Math.Abs(bbMin.Z - bbMax.Z);

            float surfaceArea = xDist * yDist * 2 + xDist * zDist * 2 + yDist * zDist * 2;

            SplitPlane plane = SplitPlane.X;

            if (xDist >= yDist && xDist >= zDist)
                plane = SplitPlane.X;

            if (yDist > xDist && yDist >= zDist)
                plane = SplitPlane.Y;

            if (zDist > xDist && zDist > yDist)
                plane = SplitPlane.Z;

            float distance = 0;
            float start = 0;
            float bestFurthestLeft = float.PositiveInfinity;
            float bestFurthestRight = float.NegativeInfinity;
            switch (plane)
            {
                case SplitPlane.X:
                    distance = xDist;
                    start = bbMin.X;
                    bestFurthestLeft = bbMin.X;
                    bestFurthestRight = bbMax.X;
                    break;
                case SplitPlane.Y:
                    distance = yDist;
                    start = bbMin.Y;
                    bestFurthestLeft = bbMin.Y;
                    bestFurthestRight = bbMax.Y;
                    break;
                case SplitPlane.Z:
                    distance = zDist;
                    start = bbMin.Z;
                    bestFurthestLeft = bbMin.Z;
                    bestFurthestRight = bbMax.Z;
                    break;
            }

            float binSize = distance / BinCount;

            float bestSplitCost = float.PositiveInfinity;
            float bestCostLeft = float.PositiveInfinity;
            float bestCostRight = float.PositiveInfinity;
            
            (Vector3, Vector3) boundingLeft = (new Vector3(), new Vector3()), boundingRight = (new Vector3(), new Vector3());
            List<Primitive> bestLeft = null;
            List<Primitive> bestRight = null;

            for (int i = 0; i < BinCount; i++)
            {
                var middle = start + binSize * i + binSize / 2f;
                float furthestLeft = middle;
                float furthestRight = middle;
                List<Primitive> left = new List<Primitive>();
                List<Primitive> right = new List<Primitive>();

                for (int j = 0; j < Primitives.Count; j++)
                {
                    var prim = Primitives[j];
                    switch (plane)
                    {
                        case SplitPlane.X:
                            if (prim.Centroid.X < middle)
                            {
                                if (prim.BoundingBox.Item2.X > furthestRight)
                                    furthestRight = prim.BoundingBox.Item2.X;
                                left.Add(prim);
                            }
                            else
                            {
                                if (prim.BoundingBox.Item1.X < furthestLeft)
                                    furthestLeft = prim.BoundingBox.Item1.X;
                                right.Add(prim);
                            }
                            break;
                        case SplitPlane.Y:
                            if (prim.Centroid.Y < middle)
                            {
                                if (prim.BoundingBox.Item2.Y > furthestRight)
                                    furthestRight = prim.BoundingBox.Item2.Y;
                                left.Add(prim);
                            }
                            else
                            {
                                if (prim.BoundingBox.Item1.Y < furthestLeft)
                                    furthestLeft = prim.BoundingBox.Item1.Y;
                                right.Add(prim);
                            }
                            break;
                        case SplitPlane.Z:
                            if (prim.Centroid.Z < middle)
                            {
                                if (prim.BoundingBox.Item2.Z > furthestRight)
                                    furthestRight = prim.BoundingBox.Item2.Z;
                                left.Add(prim);
                            }
                            else
                            {
                                if (prim.BoundingBox.Item1.Z < furthestLeft)
                                    furthestLeft = prim.BoundingBox.Item1.Z;
                                right.Add(prim);
                            }
                            break;
                    }
                }

                float surfaceAreaLeft = 0f;
                float surfaceAreaRight = 0f;

                float leftXDist;
                float rightXDist;
                float leftYDist;
                float rightYDist;
                float leftZDist;
                float rightZDist;
                switch (plane)
                {
                    case SplitPlane.X:
                        leftXDist = Math.Abs(furthestRight - bbMin.X);
                        rightXDist = Math.Abs(bbMax.X - furthestLeft);
                        surfaceAreaLeft = leftXDist * yDist * 2 + leftXDist * zDist * 2 + yDist * zDist * 2;
                        surfaceAreaRight = rightXDist * yDist * 2 + rightXDist * zDist * 2 + yDist * zDist * 2;
                        break;
                    case SplitPlane.Y:
                        leftYDist = Math.Abs(furthestRight - bbMin.Y);
                        rightYDist = Math.Abs(bbMax.Y - furthestLeft);
                        surfaceAreaLeft = xDist * leftYDist * 2 + xDist * zDist * 2 + leftYDist * zDist * 2;
                        surfaceAreaRight = xDist * rightYDist * 2 + xDist * zDist * 2 + rightYDist * zDist * 2;
                        break;
                    case SplitPlane.Z:
                        leftZDist = Math.Abs(furthestRight - bbMin.Z);
                        rightZDist = Math.Abs(bbMax.Z - furthestLeft);
                        surfaceAreaLeft = xDist * yDist * 2 + xDist * leftZDist * 2 + yDist * leftZDist * 2;
                        surfaceAreaRight = xDist * yDist * 2 + xDist * rightZDist * 2 + yDist * rightZDist * 2;
                        break;
                }

                var costLeft = CalculateCosts(left);
                var costRight = CalculateCosts(right);

                var cost = 0.125f + surfaceAreaLeft / surfaceAreaRight * costLeft + surfaceAreaRight / surfaceArea * costRight;

                if (cost < bestSplitCost)
                {
                    bestSplitCost = cost;
                    bestCostLeft = costLeft;
                    bestCostRight = costRight;
                    bestFurthestLeft = furthestLeft;
                    bestFurthestRight = furthestRight;
                    bestLeft = left;
                    bestRight = right;
                }
            }

            
            switch(plane)
            {
                case SplitPlane.X:
                    boundingLeft = (new Vector3(bbMin.X, bbMin.Y, bbMin.Z), new Vector3(bestFurthestRight, bbMax.Y, bbMax.Z));
                    boundingRight = (new Vector3(bestFurthestLeft, bbMin.Y, bbMin.Z), new Vector3(bbMax.X, bbMax.Y, bbMax.Z));
                    break;
                case SplitPlane.Y:
                    boundingLeft = (new Vector3(bbMin.X, bbMin.Y, bbMin.Z), new Vector3(bbMax.X, bestFurthestRight, bbMax.Z));
                    boundingRight = (new Vector3(bbMin.X, bestFurthestLeft, bbMin.Z), new Vector3(bbMax.X, bbMax.Y, bbMax.Z));
                    break;
                case SplitPlane.Z:
                    boundingLeft = (new Vector3(bbMin.X, bbMin.Y, bbMin.Z), new Vector3(bbMax.X, bbMax.Y, bestFurthestRight));
                    boundingRight = (new Vector3(bbMin.X, bbMin.Y, bestFurthestLeft), new Vector3(bbMax.X, bbMax.Y, bbMax.Z));
                    break;
            }
                
            Left = new BVH(bestLeft) { SplitCost = bestCostLeft, BoundingBox = boundingLeft, CurrentSplitDepth = CurrentSplitDepth + 1 };
            Right = new BVH(bestRight) { SplitCost = bestCostRight, BoundingBox = boundingRight, CurrentSplitDepth = CurrentSplitDepth + 1};

            Left.SubDivide();
            Right.SubDivide();            
        }

        private float CalculateCosts(List<Primitive> primitives)
        {
            return primitives.Count;
        }

        public override void GetTexture(Intersection intersection)
        {
            throw new NotImplementedException();
        }
    }

    public enum SplitPlane
    {
        X,
        Y,
        Z
    }
}
