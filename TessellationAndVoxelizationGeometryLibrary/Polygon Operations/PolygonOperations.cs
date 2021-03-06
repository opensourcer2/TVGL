﻿using ClipperLib;
using StarMathLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TVGL
{
    using Path = List<Point>;
    using PathAsLight = List<PointLight>;
    using Paths = List<List<Point>>;
    using PathsAsLight = List<List<PointLight>>;
    using Polygons = List<Polygon>;
    using PolygonsAsLight = List<PolygonLight>;

    internal enum BooleanOperationType
    {
        Union,
        Intersection
    };

    public enum PolygonFillType //http://www.angusj.com/delphi/clipper/documentation/Docs/Units/ClipperLib/Types/PolyFillType.htm
    {
        Positive, // (Most common if polygons are ordered correctly and not self-intersecting) All sub-regions with winding counts > 0 are filled.
        EvenOdd,  // (Most common when polygon directions are unknown) Odd numbered sub-regions are filled, while even numbered sub-regions are not.
        Negative, // (Rarely used) All sub-regions with winding counts < 0 are filled.
        NonZero //(Common if polygon directions are unknown) All non-zero sub-regions are filled (used in silhouette because we prefer filled regions).
    };

    /// <summary>
    /// A set of general operation for points and paths
    /// </summary>
    public class PolygonOperations
    {
        #region Line Intersections with Polygon

        public static List<double[]> AllPolygonIntersectionPointsAlongLine(IEnumerable<PolygonLight> polygons, double[] lineReference, double lineDirection,
              int numSteps, double stepSize, out int firstIntersectingIndex)
        {
            return AllPolygonIntersectionPointsAlongLine(polygons.Select(p => new Polygon(p, true)), lineReference,
                lineDirection, numSteps, stepSize, out firstIntersectingIndex);
        }
        public static List<double[]> AllPolygonIntersectionPointsAlongLine(IEnumerable<Polygon> polygons, double[] lineReference, double lineDirection,
              int numSteps, double stepSize, out int firstIntersectingIndex)
        {
            throw new NotImplementedException();
        }
        public static List<double[]> AllPolygonIntersectionPointsAlongX(IEnumerable<PolygonLight> polygons, double startingXValue,
              int numSteps, double stepSize, out int firstIntersectingIndex)
        {
            return AllPolygonIntersectionPointsAlongX(polygons.Select(p => new Polygon(p, true)), startingXValue,
                numSteps, stepSize, out firstIntersectingIndex);
        }
        public static List<double[]> AllPolygonIntersectionPointsAlongX(IEnumerable<Polygon> polygons, double startingXValue,
              int numSteps, double stepSize, out int firstIntersectingIndex)
        {
            var intersections = new List<double[]>();
            var sortedPoints = polygons.SelectMany(polygon => polygon.Path).OrderBy(p => p.X).ToList();
            var currentLines = new HashSet<Line>();
            var nextDistance = sortedPoints.First().X;
            firstIntersectingIndex = (int)Math.Ceiling((nextDistance - startingXValue) / stepSize);
            var pIndex = 0;
            for (int i = firstIntersectingIndex; i < numSteps; i++)
            {
                var x = startingXValue + i * stepSize;
                var thisPoint = sortedPoints[pIndex];
                var needToOffset = false;
                while (thisPoint.X <= x)
                {
                    if (x.IsPracticallySame(thisPoint.X)) needToOffset = true;
                    foreach (var line in thisPoint.Lines)
                    {
                        if (currentLines.Contains(line)) currentLines.Remove(line);
                        else currentLines.Add(line);
                    }
                    pIndex++;
                    if (pIndex == sortedPoints.Count) return intersections;
                    thisPoint = sortedPoints[pIndex];
                }
                if (needToOffset)
                    x += Math.Min(stepSize, sortedPoints[pIndex + 1].X) / 10.0;
                var numIntersects = currentLines.Count;
                var intersects = new double[numIntersects];
                var index = 0;
                foreach (var line in currentLines)
                    intersects[index++] = line.YGivenX(x);
                intersections.Add(intersects.OrderBy(y => y).ToArray());
            }
            return intersections;
        }
        public static List<double[]> AllPolygonIntersectionPointsAlongY(IEnumerable<PolygonLight> polygons, double startingYValue, int numSteps, double stepSize,
              out int firstIntersectingIndex)
        {
            return AllPolygonIntersectionPointsAlongY(polygons.Select(p => new Polygon(p, true)), startingYValue,
                numSteps, stepSize, out firstIntersectingIndex);
        }
        public static List<double[]> AllPolygonIntersectionPointsAlongY(IEnumerable<Polygon> polygons, double startingYValue, int numSteps, double stepSize,
                out int firstIntersectingIndex)
        {
            var intersections = new List<double[]>();
            var sortedPoints = polygons.SelectMany(polygon => polygon.Path).OrderBy(p => p.Y).ToList();
            var currentLines = new HashSet<Line>();
            var nextDistance = sortedPoints.First().Y;
            firstIntersectingIndex = (int)Math.Ceiling((nextDistance - startingYValue) / stepSize);
            var pIndex = 0;
            for (int i = firstIntersectingIndex; i < numSteps; i++)
            {
                var y = startingYValue + i * stepSize;
                var thisPoint = sortedPoints[pIndex];
                var needToOffset = false;
                while (thisPoint.Y <= y)
                {
                    if (y.IsPracticallySame(thisPoint.Y)) needToOffset = true;
                    foreach (var line in thisPoint.Lines)
                    {
                        if (currentLines.Contains(line)) currentLines.Remove(line);
                        else currentLines.Add(line);
                    }
                    pIndex++;
                    if (pIndex == sortedPoints.Count) return intersections;
                    thisPoint = sortedPoints[pIndex];
                }
                if (needToOffset)
                    y += Math.Min(stepSize, sortedPoints[pIndex + 1].Y) / 10.0;

                var numIntersects = currentLines.Count;
                var intersects = new double[numIntersects];
                var index = 0;
                foreach (var line in currentLines)
                    intersects[index++] = line.XGivenY(y);
                intersections.Add(intersects.OrderBy(x => x).ToArray());
            }
            return intersections;
        }
        #endregion

        /// <summary>
        /// Gets whether a polygon is rectangular by using the minimum bounding rectangle.
        /// The rectangle my be in any orientation and contain any number of points greater than three.
        /// Confidence Percentage can be decreased to identify polygons that are close to rectangular.
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="dimensions"></param>
        /// <param name="confidencePercentage"></param>
        /// <returns></returns>
        public static bool IsRectangular(PolygonLight polygon, out double[] dimensions, double confidencePercentage = Constants.HighConfidence)
        {
            if (confidencePercentage > 1.0 || Math.Sign(confidencePercentage) < 0)
                throw new Exception("Confidence percentage must be between 0 and 1");
            var tolerancePercentage = 1.0 - confidencePercentage;
            //For it to be rectangular, Area = l*w && Perimeter = 2*l + 2*w.
            //This can only gaurantee that it is not a rectangle if false.
            //If true, then check the polygon area vs. its minBoundingRectangle area. 
            //The area / perimeter check is not strictly necessary, but can provide some speed-up
            //For obviously not rectangular pieces
            var p = polygon.Length;
            var sqrRootTerm = Math.Sqrt(p * p - 16 * polygon.Area);
            var length = 0.25 * (p + sqrRootTerm);
            var width = 0.25 * (p - sqrRootTerm);
            dimensions = new[] { length, width };
            var areaCheck = length * width;
            var perimeterCheck = 2 * length + 2 * width;
            if (!polygon.Area.IsPracticallySame(areaCheck, polygon.Area * tolerancePercentage) &&
                !polygon.Length.IsPracticallySame(perimeterCheck, polygon.Length * tolerancePercentage))
            {
                return false;
            }

            var minBoundingRectangle = MinimumEnclosure.BoundingRectangle(polygon.Path);
            return polygon.Area.IsPracticallySame(minBoundingRectangle.Area, polygon.Area * tolerancePercentage);
        }

        public static bool IsCircular(PolygonLight polygon, double confidencePercentage = Constants.HighConfidence)
        {
            return IsCircular(new Polygon(polygon), out var _, confidencePercentage);
        }

        public static bool IsCircular(Polygon polygon, double confidencePercentage = Constants.HighConfidence)
        {
            return IsCircular(polygon, out var _, confidencePercentage);
        }

        public static bool IsCircular(Polygon polygon, out BoundingCircle minCircle, double confidencePercentage = Constants.HighConfidence)
        {
            var tolerancePercentage = 1.0 - confidencePercentage;
            minCircle = MinimumEnclosure.MinimumCircle(polygon.Path);

            //Check if areas are close to the same
            var polygonArea = Math.Abs(polygon.Area);
            return polygonArea.IsPracticallySame(minCircle.Area, polygonArea * tolerancePercentage);
        }

        /// <summary>
        /// Gets the length of a path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static double Length(IList<PointLight> path)
        {
            if (path.Count < 2) return 0.0;
            var editPath = new List<PointLight>(path) { path.First() };
            var length = 0.0;
            for (var i = 0; i < editPath.Count - 1; i++)
            {
                var p1 = editPath[i];
                var p2 = editPath[i + 1];
                length += MiscFunctions.DistancePointToPoint(p1, p2);
            }
            return length;
        }

        /// <summary>
        /// Gets the length of a path
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static double Length(IList<List<PointLight>> paths)
        {
            return paths.Sum(path => Length(path));
        }

        /// <summary>
        /// Gets the Shallow Polygon Trees for a given set of paths. 
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static List<ShallowPolygonTree> GetShallowPolygonTrees(List<List<Point>> paths)
        {
            return ShallowPolygonTree.GetShallowPolygonTrees(paths);
        }
        public static List<ShallowPolygonTree> GetShallowPolygonTrees(List<List<PointLight>> paths)
        {
            return ShallowPolygonTree.GetShallowPolygonTrees(paths
                .Select(path => path.Select(p => new Point(p)).ToList()).ToList());
        }
        public static List<ShallowPolygonTree> GetShallowPolygonTrees(Polygons paths)
        {
            return ShallowPolygonTree.GetShallowPolygonTrees(paths);
        }
        public static List<ShallowPolygonTree> GetShallowPolygonTrees(IEnumerable<PolygonLight> paths)
        {
            return ShallowPolygonTree.GetShallowPolygonTrees(paths.Select(p => new Polygon(p)).ToList());
        }

        #region Clockwise / CounterClockwise Ordering

        /// <summary>
        /// Sets a polygon to counter clock wise positive
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        /// <assumptions>
        /// 1. the polygon is closed
        /// 2. the last point is not repeated.
        /// 3. the polygon is simple (does not intersect itself or have holes)
        /// </assumptions>
        public static List<Point> CCWPositive(IList<Point> p)
        {
            var polygon = new List<Point>(p);
            var area = MiscFunctions.AreaOfPolygon(p.ToArray());
            if (area < 0) polygon.Reverse();
            return polygon;
        }

        /// <summary>
        /// Sets a polygon to clock wise negative
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static List<Point> CWNegative(IList<Point> p)
        {
            var polygon = new List<Point>(p);
            var area = MiscFunctions.AreaOfPolygon(p.ToArray());
            if (area > 0) polygon.Reverse();
            return polygon;
        }

        #endregion

        #region Simplify

        public static List<List<PointLight>> SimplifyFuzzy(IList<List<PointLight>> paths)
        {
            return paths.Select(SimplifyFuzzy).ToList();
        }

        public static List<List<PointLight>> SimplifyFuzzy(IList<List<PointLight>> paths, double lengthTolerance,
            double slopeTolerance)
        {
            return paths.Select(p => SimplifyFuzzy(p, lengthTolerance, slopeTolerance)).ToList();
        }

        /// <summary>
        /// Simplifies the lines on a polygon to use fewer points when possible.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<PointLight> SimplifyFuzzy(IList<PointLight> path)
        {
            const double lengthTolerance = Constants.LineLengthMinimum;
            const double slopeTolerance = Constants.LineSlopeTolerance;
            return SimplifyFuzzy(path, lengthTolerance, slopeTolerance);
        }

        /// <summary>
        /// Simplifies the lines on a polygon to use fewer points when possible.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="lengthTolerance"></param>
        /// <param name="slopeTolerance"></param>
        /// <returns></returns>
        public static List<PointLight> SimplifyFuzzy(IList<PointLight> path, double lengthTolerance,
            double slopeTolerance)
        {
            if (lengthTolerance.IsNegligible()) lengthTolerance = Constants.LineLengthMinimum;
            var squareLengthTolerance = lengthTolerance * lengthTolerance;
            var n = path.Count;
            if (n < 4) return new List<PointLight>(path);
            var simplePath = new List<PointLight>(path);

            //Remove negligible length lines and combine collinear lines.
            var i = 0;
            var j = 1;
            var k = 2;
            var iX = simplePath[i].X;
            var iY = simplePath[i].Y;
            var jX = simplePath[j].X;
            var jY = simplePath[j].Y;
            var kX = simplePath[k].X;
            var kY = simplePath[k].Y;
            while (i < n)
            {
                //The simplification is destroying this polygon. Do not simplify it.
                if (n < 4) return new List<PointLight>(path);

                //We only check line I-J in the first iteration, since later we
                //check line J-K instead.
                if (i == 0 && NegligibleLine(iX, iY, jX, jY, squareLengthTolerance))
                {
                    simplePath.RemoveAt(j);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                    //Current stays the same.
                    //j moves to k, k moves forward but has the same index.
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                else if (NegligibleLine(jX, jY, kX, kY, squareLengthTolerance))
                {
                    simplePath.RemoveAt(k);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                    //Current and Next stay the same.
                    //k moves forward but has the same index.
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                //Use an even looser tolerance to determine if slopes are equal.
                else if (LineSlopesEqual(iX, iY, jX, jY, kX, kY, slopeTolerance))
                {
                    simplePath.RemoveAt(j);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 

                    //Current stays the same.
                    //j moves to k, k moves forward but has the same index.
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                else
                {
                    //Everything moves forward
                    i++;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                    iX = jX;
                    iY = jY;
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
            }

            var area1 = MiscFunctions.AreaOfPolygon(path);
            var area2 = MiscFunctions.AreaOfPolygon(simplePath);

            //If the simplification destroys a polygon, do not simplify it.
            if (area2.IsNegligible() ||
                !area1.IsPracticallySame(area2, Math.Abs(area1 * (1 - Constants.HighConfidence))))
            {
                return (PathAsLight)path;
            }

            return simplePath;
        }

        /// <summary>
        /// Simplifies the lines on a polygon to use fewer points when possible.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="edgeLength"></param>
        /// <returns></returns>
        public static List<PointLight> SampleWithEdgeLength(IList<PointLight> path, double edgeLength)
        {
            //ToDo: Speed this up to be like SimplifyFuzzy
            var lengthTolerance = edgeLength / 2;
            var squareLengthTolerance = lengthTolerance * lengthTolerance;

            var newPath = new List<PointLight>();
            var simplePath = new List<PointLight>(path);
            for (var i = 0; i < simplePath.Count; i++)
            {
                var j = i + 1;
                if (i == simplePath.Count - 1) j = 0;
                var k = j + 1;
                if (j == simplePath.Count - 1) k = 0;
                var current = simplePath[i];
                var next = simplePath[j];
                var nextNext = simplePath[k];
                var length = MiscFunctions.DistancePointToPoint(current, next);
                if (length > edgeLength)
                {
                    var n = (int)(length / edgeLength);
                    var vector = (next - current).normalize().multiply(edgeLength);
                    newPath.Add(current);
                    for (var p = 0; p < n; p++)
                    {
                        current = new PointLight(new[] { current.X + vector[0], current.Y + vector[1] });
                        newPath.Add(current);
                    }
                }
                else if (i == 0 && NegligibleLine(current.X, current.Y, next.X, next.Y, squareLengthTolerance))
                {
                    simplePath.RemoveAt(j);
                    i--;
                    continue;
                }
                else if (NegligibleLine(next.X, next.Y, nextNext.X, nextNext.Y, squareLengthTolerance))
                {
                    simplePath.RemoveAt(k);
                    i--;
                    continue;
                }
                else
                {
                    newPath.Add(current);
                }
            }
            return newPath;
        }

        /// <summary>
        /// Simplifies the lines on a polygon to use fewer points when possible.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<Point> SimplifyFuzzy(IList<Point> path)
        {
            const double lengthTolerance = Constants.LineLengthMinimum;
            const double slopeTolerance = Constants.LineSlopeTolerance;
            return SimplifyFuzzy(path, lengthTolerance, slopeTolerance);
        }

        /// <summary>
        /// Simplifies the lines on a polygon to use fewer points when possible.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="lengthTolerance"></param>
        /// <param name="slopeTolerance"></param>
        /// <returns></returns>
        public static List<Point> SimplifyFuzzy(IList<Point> path, double lengthTolerance,
            double slopeTolerance)
        {
            if (lengthTolerance.IsNegligible()) lengthTolerance = Constants.LineLengthMinimum;
            var squareLengthTolerance = lengthTolerance * lengthTolerance;
            var simplePath = new List<Point>(path);
            var n = simplePath.Count;
            if (n < 4) return simplePath;

            //Remove negligible length lines and combine collinear lines.
            var i = 0;
            var j = 1;
            var k = 2;
            var iX = simplePath[i].X;
            var iY = simplePath[i].Y;
            var jX = simplePath[j].X;
            var jY = simplePath[j].Y;
            var kX = simplePath[k].X;
            var kY = simplePath[k].Y;
            while (i < n)
            {
                //We only check line I-J in the first iteration, since later we
                //check line J-K instead.
                if (i == 0 && NegligibleLine(iX, iY, jX, jY, squareLengthTolerance))
                {
                    simplePath.RemoveAt(j);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                    //Current stays the same.
                    //j moves to k, k moves forward but has the same index.
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                else if (NegligibleLine(jX, jY, kX, kY, squareLengthTolerance))
                {
                    simplePath.RemoveAt(k);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                    //Current and Next stay the same.
                    //k moves forward but has the same index.
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                //Use an even looser tolerance to determine if slopes are equal.
                else if (LineSlopesEqual(iX, iY, jX, jY, kX, kY, slopeTolerance))
                {
                    simplePath.RemoveAt(j);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 

                    //Current stays the same.
                    //j moves to k, k moves forward but has the same index.
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                else
                {
                    //Everything moves forward
                    i++;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                    iX = jX;
                    iY = jY;
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
            }

            var area1 = MiscFunctions.AreaOfPolygon(path);
            var area2 = MiscFunctions.AreaOfPolygon(simplePath);

            //If the simplification destroys a polygon, do not simplify it.
            if (area2.IsNegligible() ||
                !area1.IsPracticallySame(area2, Math.Abs(area1 * (1 - Constants.HighConfidence))))
            {
                return (Path)path;
            }

            return simplePath;
        }


        private static bool NegligibleLine(double p1X, double p1Y, double p2X, double p2Y, double squaredTolerance)
        {
            var dX = p1X - p2X;
            var dY = p1Y - p2Y;
            return (dX * dX + dY * dY).IsNegligible(squaredTolerance);
        }

        private static bool LineSlopesEqual(double p1X, double p1Y, double p2X, double p2Y, double p3X, double p3Y,
            double tolerance = Constants.LineSlopeTolerance)
        {
            if (tolerance.IsNegligible()) tolerance = Constants.LineSlopeTolerance;
            var value = (p1Y - p2Y) * (p2X - p3X) - (p1X - p2X) * (p2Y - p3Y);
            return value.IsNegligible(tolerance);
        }
        #endregion

        #region Offset

        /// <summary>
        /// Offets the given path by the given offset, rounding corners.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="offset"></param>
        /// <param name="minLength"></param>
        /// <returns></returns>
        public static List<List<PointLight>> OffsetRound(IEnumerable<PointLight> path, double offset,
            double minLength = 0.0)
        {
            return Offset(new PathsAsLight { path.ToList() }, offset, JoinType.jtRound, minLength);
        }
        public static PolygonsAsLight OffsetRound(PolygonLight path, double offset,
            double minLength = 0.0)
        {
            return Offset(new PolygonsAsLight { path }, offset, JoinType.jtRound, minLength);
        }

        /// <summary>
        /// Offsets all paths by the given offset value. Rounds the corners.
        /// Offest value may be positive or negative.
        /// Loops must be ordered CCW positive.
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="offset"></param>
        /// <param name="minLength"></param>
        /// <returns></returns>
        public static List<List<PointLight>> OffsetRound(IEnumerable<IEnumerable<PointLight>> paths, double offset,
            double minLength = 0.0)
        {
            var listPaths = paths.Select(path => path.ToList()).ToList();
            return Offset(listPaths, offset, JoinType.jtRound, minLength);
        }
        public static PolygonsAsLight OffsetRound(PolygonsAsLight paths, double offset, double minLength = 0.0)
        {
            return Offset(paths, offset, JoinType.jtRound, minLength);
        }

        /// <summary>
        /// Offsets all paths by the given offset value. Squares the corners.
        /// Offest value may be positive or negative. 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="offset"></param>
        /// <param name="minLength"></param>
        /// <returns></returns>
        public static List<List<PointLight>> OffsetMiter(IEnumerable<PointLight> path, double offset, double minLength = 0.0)
        {
            return Offset(new PathsAsLight { path.ToList() }, offset, JoinType.jtMiter, minLength);
        }
        public static List<PolygonLight> OffsetMiter(PolygonLight path, double offset, double minLength = 0.0)
        {
            return Offset(new PolygonsAsLight { path }, offset, JoinType.jtMiter, minLength);
        }

        /// <summary>
        /// Offsets all paths by the given offset value. Miters the corners.
        /// Offest value may be positive or negative.
        /// Loops must be ordered CCW positive.
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="minLength"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static List<List<PointLight>> OffsetMiter(IEnumerable<IEnumerable<PointLight>> paths, double offset, double minLength = 0.0)
        {
            var listPaths = paths.Select(path => path.ToList()).ToList();
            return Offset(listPaths, offset, JoinType.jtMiter, minLength);
        }
        public static PolygonsAsLight OffsetMiter(PolygonsAsLight paths, double offset, double minLength = 0.0)
        {
            return Offset(paths, offset, JoinType.jtMiter, minLength);
        }

        /// <summary>
        /// Offsets all paths by the given offset value. Squares the corners.
        /// Offest value may be positive or negative. 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="offset"></param>
        /// <param name="minLength"></param>
        /// <returns></returns>
        public static List<List<PointLight>> OffsetSquare(List<PointLight> path, double offset, double minLength = 0.0)
        {
            return Offset(new PathsAsLight { path.ToList() }, offset, JoinType.jtSquare, minLength);
        }
        public static PolygonsAsLight OffsetSquare(PolygonLight path, double offset, double minLength = 0.0)
        {
            return Offset(new PolygonsAsLight { path }, offset, JoinType.jtSquare, minLength);
        }

        /// <summary>
        /// Offsets all paths by the given offset value. Squares the corners.
        /// Offest value may be positive or negative.
        /// Loops must be ordered CCW positive.
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="minLength"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static List<List<PointLight>> OffsetSquare(List<List<PointLight>> paths, double offset, double minLength = 0.0)
        {
            var listPaths = paths.Select(path => path.ToList()).ToList();
            return Offset(listPaths, offset, JoinType.jtSquare, minLength);
        }
        public static PolygonsAsLight OffsetSquare(PolygonsAsLight paths, double offset, double minLength = 0.0)
        {
            return Offset(paths, offset, JoinType.jtSquare, minLength);
        }

        private static List<List<PointLight>> Offset(List<List<PointLight>> paths, double offset, JoinType joinType,
            double minLength = 0.0)
        {
            const double scale = 1000000;
            if (minLength.IsNegligible())
            {
                var totalLength = paths.Sum(loop => Length(loop));
                minLength = totalLength * 0.001;
            }

            //Convert Points (TVGL) to IntPoints (Clipper)
            var clipperSubject =
                paths.Select(loop => loop.Select(point => new IntPoint(point.X * scale, point.Y * scale)).ToList()).ToList();

            //Setup Clipper
            var clip = new ClipperOffset(2, minLength * scale);
            clip.AddPaths(clipperSubject, joinType, EndType.etClosedPolygon);

            //Begin an evaluation
            var clipperSolution = new List<List<IntPoint>>();
            clip.Execute(clipperSolution, offset * scale);

            //Convert back to points and return solution
            var solution = clipperSolution.Select(clipperPath => clipperPath.Select(point => new PointLight(point.X / scale, point.Y / scale)).ToList()).ToList();
            return solution;
        }
        private static PolygonsAsLight Offset(IEnumerable<PolygonLight> polygons, double offset, JoinType joinType,
            double minLength = 0.0)
        {
            return Offset(polygons.Select(p => p.Path).ToList(), offset, joinType, minLength)
                .Select(path => new PolygonLight(path)).ToList();
        }
        #endregion

        #region Boolean Operations

        #region Union

        /// <summary>
        /// Union. Joins paths that are touching into merged larger subject.
        /// Use GetShallowPolygonTrees to correctly order the polygons inside one another.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="simplifyPriorToUnion"></param>
        /// <param name="polyFill"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Union(IList<List<PointLight>> subject, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, subject, null, simplifyPriorToUnion);
        }
        public static PolygonsAsLight Union(PolygonsAsLight subject, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, subject, null, simplifyPriorToUnion);
        }

        /// <summary>
        /// Union. Joins paths that are touching into merged larger subject.
        /// Use GetShallowPolygonTrees to correctly order the polygons inside one another.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToUnion"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Union(IList<List<PointLight>> subject, IList<List<PointLight>> clip, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, subject, clip, simplifyPriorToUnion);
        }
        public static PolygonsAsLight Union(PolygonsAsLight subject, PolygonsAsLight clip, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, subject, clip, simplifyPriorToUnion);
        }

        /// <summary>
        /// Union. Joins paths that are touching into merged larger subject.
        /// Use GetShallowPolygonTrees to correctly order the polygons inside one another.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToUnion"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Union(List<PointLight> subject, List<PointLight> clip, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, new PathsAsLight { subject }, new PathsAsLight { clip }, simplifyPriorToUnion);
        }
        public static PolygonsAsLight Union(PolygonLight subject, PolygonLight clip, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, new PolygonsAsLight { subject }, new PolygonsAsLight { clip }, simplifyPriorToUnion);
        }

        /// <summary>
        /// Union. Joins paths that are touching into merged larger subject.
        /// Use GetShallowPolygonTrees to correctly order the polygons inside one another.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToUnion"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Union(IList<List<PointLight>> subject, List<PointLight> clip, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, subject, new PathsAsLight { clip }, simplifyPriorToUnion);
        }
        public static PolygonsAsLight Union(PolygonsAsLight subject, PolygonLight clip, bool simplifyPriorToUnion = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctUnion, subject, new PolygonsAsLight { clip }, simplifyPriorToUnion);
        }
        #endregion

        #region Difference
        /// <summary>
        /// Difference. Gets the difference between two sets of polygons. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToDifference"></param>
        /// <param name="polyFill"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Difference(IList<List<PointLight>> subject, IList<List<PointLight>> clip, bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, subject, clip, simplifyPriorToDifference);
        }
        public static List<PolygonLight> Difference(IList<PolygonLight> subject, IList<PolygonLight> clip, bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, subject, clip, simplifyPriorToDifference);
        }

        /// <summary>
        /// Difference. Gets the difference between two sets of polygons. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToDifference"></param>
        /// <param name="polyFill"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Difference(List<PointLight> subject, List<PointLight> clip, bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, new PathsAsLight { subject }, new PathsAsLight { clip }, simplifyPriorToDifference);
        }
        public static PolygonsAsLight Difference(PolygonLight subject, PolygonLight clip, bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, new PolygonsAsLight { subject }, new PolygonsAsLight { clip }, simplifyPriorToDifference);
        }

        /// <summary>
        /// Difference. Gets the difference between two sets of polygons. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToDifference"></param>
        /// <param name="polyFill"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Difference(IList<List<PointLight>> subject, List<PointLight> clip, bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, subject, new PathsAsLight { clip }, simplifyPriorToDifference);
        }
        public static PolygonsAsLight Difference(PolygonsAsLight subject, PolygonLight clip, bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, subject, new PolygonsAsLight { clip }, simplifyPriorToDifference);
        }

        /// <summary>
        /// Difference. Gets the difference between two sets of polygons. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToDifference"></param>
        /// <param name="polyFill"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Difference(List<PointLight> subject, IList<List<PointLight>> clip,
            bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, new PathsAsLight { subject }, clip, simplifyPriorToDifference);
        }
        public static PolygonsAsLight Difference(PolygonLight subject, PolygonsAsLight clip,
            bool simplifyPriorToDifference = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctDifference, new PolygonsAsLight { subject }, clip, simplifyPriorToDifference);
        }
        #endregion

        #region Intersection
        /// <summary>
        /// Intersection. Gets the areas covered by both the subjects and the clips. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToIntersection"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Intersection(List<PointLight> subject, List<PointLight> clip, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Intersection(new PathsAsLight { subject }, new PathsAsLight { clip }, simplifyPriorToIntersection, polyFill);
        }
        public static PolygonsAsLight Intersection(PolygonLight subject, PolygonLight clip, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Intersection(new PolygonsAsLight { subject }, new PolygonsAsLight { clip }, simplifyPriorToIntersection, polyFill);
        }

        /// <summary>
        /// Intersection. Gets the areas covered by both the subjects and the clips.
        /// </summary>
        /// <param name="subjects"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToIntersection"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Intersection(IList<List<PointLight>> subjects, List<PointLight> clip, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Intersection(new PathsAsLight(subjects), new PathsAsLight { clip }, simplifyPriorToIntersection, polyFill);
        }
        public static PolygonsAsLight Intersection(PolygonsAsLight subjects, PolygonLight clip, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Intersection(subjects, new PolygonsAsLight { clip }, simplifyPriorToIntersection, polyFill);
        }

        /// <summary>
        /// Intersection. Gets the areas covered by both the subjects and the clips. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clips"></param>
        /// <param name="simplifyPriorToIntersection"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Intersection(List<PointLight> subject, IList<List<PointLight>> clips, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Intersection(new List<List<PointLight>>() { subject }, new List<List<PointLight>>(clips), simplifyPriorToIntersection, polyFill);
        }
        public static PolygonsAsLight Intersection(PolygonLight subject, List<PolygonLight> clips, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Intersection(new PolygonsAsLight { subject }, clips, simplifyPriorToIntersection, polyFill);
        }

        /// <summary>
        /// Intersection. Gets the areas covered by both the subjects and the clips.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToIntersection"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Intersection(IList<List<PointLight>> subject, IList<List<PointLight>> clip, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctIntersection, subject, clip, simplifyPriorToIntersection);
        }
        public static List<PolygonLight> Intersection(IList<PolygonLight> subject, IList<PolygonLight> clip, bool simplifyPriorToIntersection = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctIntersection, subject, clip, simplifyPriorToIntersection);
        }
        #endregion

        #region Xor

        /// <summary>
        /// XOR. Opposite of Intersection. Gets the areas covered by only either subjects or clips. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToXor"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Xor(IList<List<PointLight>> subject, IList<List<PointLight>> clip, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctXor, subject, clip, simplifyPriorToXor);
        }
        public static PolygonsAsLight Xor(PolygonsAsLight subject, PolygonsAsLight clip, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return BooleanOperation(polyFill, ClipType.ctXor, subject, clip, simplifyPriorToXor);
        }

        /// <summary>
        /// XOR. Opposite of Intersection. Gets the areas covered by only either subjects or clips. 
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToXor"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Xor(List<PointLight> subject, List<PointLight> clip, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Xor(new List<List<PointLight>>() { subject }, new List<List<PointLight>>() { clip }, simplifyPriorToXor, polyFill);
        }
        public static PolygonsAsLight Xor(PolygonLight subject, PolygonLight clip, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Xor(new PolygonsAsLight { subject }, new PolygonsAsLight { clip }, simplifyPriorToXor, polyFill);
        }

        /// <summary>
        /// XOR. Opposite of Intersection. Gets the areas covered by only either subjects or clips. 
        /// </summary>
        /// <param name="subjects"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToXor"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Xor(IList<List<PointLight>> subjects, List<PointLight> clip, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Xor(new List<List<PointLight>>(subjects), new List<List<PointLight>>() { clip }, simplifyPriorToXor, polyFill);
        }
        public static PolygonsAsLight Xor(PolygonsAsLight subjects, PolygonLight clip, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Xor(subjects, new PolygonsAsLight { clip }, simplifyPriorToXor, polyFill);
        }

        /// <summary>
        /// XOR. Opposite of Intersection. Gets the areas covered by only either subjects or clips.  
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="clips"></param>
        /// <param name="simplifyPriorToXor"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static List<List<PointLight>> Xor(List<PointLight> subject, IList<List<PointLight>> clips, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Xor(new List<List<PointLight>>() { subject }, new List<List<PointLight>>(clips), simplifyPriorToXor, polyFill);
        }
        public static PolygonsAsLight Xor(PolygonLight subject, PolygonsAsLight clips, bool simplifyPriorToXor = true, PolygonFillType polyFill = PolygonFillType.Positive)
        {
            return Xor(new PolygonsAsLight { subject }, clips, simplifyPriorToXor, polyFill);
        }

        #endregion

        private static PolygonsAsLight BooleanOperation(PolygonFillType fillMethod, ClipType clipType,
            IEnumerable<PolygonLight> subject,
            IEnumerable<PolygonLight> clip = null, bool simplifyPriorToBooleanOperation = true, double scale = 1000000)
        {
            var clipPaths = clip?.Select(p => p.Path).ToList(); //Handle null clip
            var paths = BooleanOperation(fillMethod, clipType, subject.Select(p => p.Path).ToList(),
                clipPaths, simplifyPriorToBooleanOperation, scale);
            return paths.Select(path => new PolygonLight(path)).ToList();
        }
        private static List<List<PointLight>> BooleanOperation(PolygonFillType fillMethod, ClipType clipType, IEnumerable<PathAsLight> subject,
           IEnumerable<PathAsLight> clip = null, bool simplifyPriorToBooleanOperation = true, double scale = 1000000)
        {
            //Convert the fill type from PolygonOperations wrapper to Clipper enum types
            PolyFillType fillType;
            switch (fillMethod)
            {
                case PolygonFillType.Positive:
                    fillType = PolyFillType.pftPositive;
                    break;
                case PolygonFillType.Negative:
                    fillType = PolyFillType.pftNegative;
                    break;
                case PolygonFillType.NonZero:
                    fillType = PolyFillType.pftNonZero;
                    break;
                case PolygonFillType.EvenOdd:
                    fillType = PolyFillType.pftEvenOdd;
                    break;
                default:
                    throw new NotImplementedException();
            }
            return BooleanOperation(fillType, clipType, subject, clip, simplifyPriorToBooleanOperation, scale);
        }


        /// <summary>
        /// Performs the Boolean Operations from the Clipper Library
        /// </summary>
        /// <param name="clipType"></param>
        /// <param name="subject"></param>
        /// <param name="clip"></param>
        /// <param name="simplifyPriorToBooleanOperation"></param>
        /// <param name="scale"></param>
        /// <param name="fillMethod"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static List<List<PointLight>> BooleanOperation(PolyFillType fillMethod, ClipType clipType, IEnumerable<PathAsLight> subject,
            IEnumerable<PathAsLight> clip = null, bool simplifyPriorToBooleanOperation = true, double scale = 1000000)
        {
            //Remove any polygons that are only a line.
            subject = subject.Where(p => p.Count > 2);
            clip = clip?.Where(p => p.Count > 2);

            if (simplifyPriorToBooleanOperation)
            {
                subject = subject.Select(SimplifyFuzzy);
            }
            if (simplifyPriorToBooleanOperation)
            {
                //If not null
                clip = clip?.Select(SimplifyFuzzy);
            }

            if (!subject.Any())
            {
                if(clip == null || !clip.Any())
                {
                    return new List<List<PointLight>>();
                }
                //Use the clip as the subject if this is a union operation and the clip is not null.
                if (clipType == ClipType.ctUnion)
                {
                    subject = clip;
                    clip = null;
                }
            }
           
            var clipperSolution = new List<List<IntPoint>>();
            //Convert Points (TVGL) to IntPoints (Clipper)
            var clipperSubject =
                subject.Select(loop => loop.Select(point => new IntPoint(point.X * scale, point.Y * scale)).ToList()).ToList();

            //Setup Clipper
            var clipper = new ClipperLib.Clipper() { StrictlySimple = true };
            clipper.AddPaths(clipperSubject, PolyType.ptSubject, true);

            if (clip != null)
            {
                var clipperClip =
                    clip.Select(loop => loop.Select(point => new IntPoint(point.X * scale, point.Y * scale)).ToList()).ToList();
                clipper.AddPaths(clipperClip, PolyType.ptClip, true);
            }

            //Begin an evaluation
            var result = clipper.Execute(clipType, clipperSolution, fillMethod, fillMethod);
            if (!result) throw new Exception("Clipper Union Failed");

            //Convert back to points
            var solution = clipperSolution.Select(clipperPath => clipperPath.Select(point => new PointLight(point.X / scale, point.Y / scale)).ToList()).ToList();
            return solution;
        }
        #endregion

        private static int NumberOfLinesBelow(SweepEvent se1, SweepList sweepLines)
        {
            var linesBelow = 0;
            //Any indices above se1 can be ignored
            for (var i = se1.IndexInList - 1; i > -1; i--)
            {
                var se2 = sweepLines.Item(i);
                var se2Y = LineIntercept(se2.Point, se2.OtherEvent.Point, se1.Point.X);
                if (IsPointOnSegment(se2.Point, se2.OtherEvent.Point, new Point(se1.Point.X, se2Y)))
                {
                    linesBelow++;
                }
            }
            return linesBelow;
        }

        #region Top Level Boolean Operation Method
        /// <reference>
        /// This aglorithm is based on on the paper:
        /// A simple algorithm for Boolean operations on polygons. Martínez, et. al. 2013. Advances in Engineering Software.
        /// Links to paper: http://dx.doi.org/10.1016/j.advengsoft.2013.04.004 OR http://www.sciencedirect.com/science/article/pii/S0965997813000379
        /// </reference>
        private static List<List<Point>> BooleanOperation(IList<List<Point>> subject, IList<List<Point>> clip, BooleanOperationType booleanOperationType)
        {
            //1.Find intersections with vertical sweep line
            //1.Subdivide the edges of the polygons at their intersection points.
            //2.Select those subdivided edges that lie inside—or outside—the other polygon.
            //3.Join the selected edges to form the contours of the result polygon and compute the child contours.
            var unsortedSweepEvents = new List<SweepEvent>();

            #region Build Sweep PathID and Order Them Lexicographically
            //Build the sweep events and order them lexicographically (Low X to High X, then Low Y to High Y).
            foreach (var path in subject)
            {
                var n = path.Count;
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    SweepEvent se1, se2;
                    path[i].IndexInPath = i;
                    path[i].InResult = false;
                    path[i].InResultMultipleTimes = false;
                    if (path[i].X.IsPracticallySame(path[j].X))
                    {
                        if (path[i].Y.IsPracticallySame(path[j].Y)) continue; //Ignore this 
                        if (path[i].Y < path[j].Y)
                        {
                            se1 = new SweepEvent(path[i], true, true, PolygonType.Subject);
                            se2 = new SweepEvent(path[j], false, false, PolygonType.Subject);
                        }
                        else
                        {
                            se1 = new SweepEvent(path[i], false, true, PolygonType.Subject);
                            se2 = new SweepEvent(path[j], true, false, PolygonType.Subject);
                        }
                    }
                    else if (path[i].X < path[j].X)
                    {
                        se1 = new SweepEvent(path[i], true, true, PolygonType.Subject);
                        se2 = new SweepEvent(path[j], false, false, PolygonType.Subject);
                    }
                    else
                    {
                        se1 = new SweepEvent(path[i], false, true, PolygonType.Subject);
                        se2 = new SweepEvent(path[j], true, false, PolygonType.Subject);
                    }
                    se1.OtherEvent = se2;
                    se2.OtherEvent = se1;
                    unsortedSweepEvents.Add(se1);
                    unsortedSweepEvents.Add(se2);
                }
            }
            foreach (var path in clip)
            {
                var n = path.Count;
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    SweepEvent se1, se2;
                    path[i].IndexInPath = i;
                    path[i].InResult = false;
                    path[i].InResultMultipleTimes = false;
                    if (path[i].X.IsPracticallySame(path[j].X))
                    {
                        if (path[i].Y.IsPracticallySame(path[j].Y)) continue; //Ignore this 
                        if (path[i].Y < path[j].Y)
                        {
                            se1 = new SweepEvent(path[i], true, true, PolygonType.Clip);
                            se2 = new SweepEvent(path[j], false, false, PolygonType.Clip);
                        }
                        else
                        {
                            se1 = new SweepEvent(path[i], false, true, PolygonType.Clip);
                            se2 = new SweepEvent(path[j], true, false, PolygonType.Clip);
                        }
                    }
                    else if (path[i].X < path[j].X)
                    {
                        se1 = new SweepEvent(path[i], true, true, PolygonType.Clip);
                        se2 = new SweepEvent(path[j], false, false, PolygonType.Clip);
                    }
                    else
                    {
                        se1 = new SweepEvent(path[i], false, true, PolygonType.Clip);
                        se2 = new SweepEvent(path[j], true, false, PolygonType.Clip);
                    }
                    se1.OtherEvent = se2;
                    se2.OtherEvent = se1;
                    unsortedSweepEvents.Add(se1);
                    unsortedSweepEvents.Add(se2);
                }
            }
            var orderedSweepEvents = new OrderedSweepEventList(unsortedSweepEvents);
            #endregion

            var result = new List<SweepEvent>();
            var sweepLines = new SweepList();
            while (orderedSweepEvents.Any())
            {
                var sweepEvent = orderedSweepEvents.First();
                orderedSweepEvents.RemoveAt(0);
                SweepEvent nextSweepEvent = null;
                if (orderedSweepEvents.Any())
                {
                    nextSweepEvent = orderedSweepEvents.First();
                }
                if (sweepEvent.Left) //left endpoint
                {

                    //Inserting the event into the sweepLines list
                    var index = sweepLines.Insert(sweepEvent);
                    sweepEvent.IndexInList = index;
                    bool goBack1; //goBack is used to processes line segments from some collinear intersections
                    CheckAndResolveIntersection(sweepEvent, sweepLines.Next(index), ref sweepLines, ref orderedSweepEvents, out goBack1);
                    bool goBack2;
                    CheckAndResolveIntersection(sweepLines.Previous(index), sweepEvent, ref sweepLines, ref orderedSweepEvents, out goBack2);
                    if (goBack1 || goBack2) continue;

                    //First, we need to check if the this sweepEvent has the same Point and is collinear with the next line. 
                    //To determine collinearity, we need to make sure we are using the same criteria as everywhere else in the code, and here-in lies the problem
                    //1. m1 != m2 but LineLineIntersection function says collinear. 
                    //2. m1 =! m2 && LineLineIntersection says non-collinear, but yintercept at shorter lines other.X, yeilds shorter line's other point.
                    //Which should we use? Should we adjust tolerances? - We need to use the least precise method, which should be the last one.
                    if (nextSweepEvent != null && nextSweepEvent.Point == sweepEvent.Point)
                    {
                        //If the slopes are practically the same then the lines are collinear 
                        //If remotely similar, we need to use the intersection, which is used later on to determine collinearity. (basically, we have to be consistent).
                        if (sweepEvent.Slope.IsPracticallySame(nextSweepEvent.Slope, 0.00001))
                        {
                            Point intersectionPoint;
                            if (MiscFunctions.LineLineIntersection(sweepEvent.Point, sweepEvent.OtherEvent.Point,
                                nextSweepEvent.Point, nextSweepEvent.OtherEvent.Point, out intersectionPoint, true) &&
                                intersectionPoint == null)
                            {
                                //If they belong to the same polygon type, they are overlapping, but we still use the other polygon like normal
                                //to determine if they are in the result.
                                if (sweepEvent.PolygonType == nextSweepEvent.PolygonType)
                                    throw new NotImplementedException();
                                sweepEvent.DuplicateEvent = nextSweepEvent;
                                nextSweepEvent.DuplicateEvent = sweepEvent;
                                SetInformation(sweepEvent, null, booleanOperationType, true);
                            }
                            else
                            {
                                //Set information updates the OtherInOut property and uses this to determine if the sweepEvent is part of the result.
                                //Select the closest edge downward that belongs to the other polygon.
                                SetInformation(sweepEvent, sweepLines.PreviousOther(index), booleanOperationType);
                            }
                        }
                        else
                        {
                            //Set information updates the OtherInOut property and uses this to determine if the sweepEvent is part of the result.
                            //Select the closest edge downward that belongs to the other polygon.
                            SetInformation(sweepEvent, sweepLines.PreviousOther(index), booleanOperationType);
                        }
                    }
                    else
                    {
                        //Select the closest edge downward that belongs to the other polygon.
                        //Set information updates the OtherInOut property and uses this to determine if the sweepEvent is part of the result.
                        SetInformation(sweepEvent, sweepLines.PreviousOther(index), booleanOperationType);
                    }
                    //Get the previous (first directly below starting point) event in result (using the sweeplines)
                    if (sweepEvent.InResult)
                    {
                        sweepEvent.PrevInResult = sweepLines.PreviousInResult(index);
                    }
                }
                else //The sweep event corresponds to the right endpoint
                {
                    var index = sweepLines.Find(sweepEvent.OtherEvent);
                    if (index == -1) throw new Exception("Other event not found in list. Error in implementation");
                    var next = sweepLines.Next(index);
                    var prev = sweepLines.Previous(index);
                    sweepLines.RemoveAt(index);
                    bool goBack;
                    CheckAndResolveIntersection(prev, next, ref sweepLines, ref orderedSweepEvents, out goBack);
                }
                if (sweepEvent.InResult || sweepEvent.OtherEvent.InResult)
                {
                    if (sweepEvent.InResult && !sweepEvent.Left) throw new Exception("error in implementation");
                    if (sweepEvent.OtherEvent.InResult && sweepEvent.Left) throw new Exception("error in implementation");
                    if (sweepEvent.Point == sweepEvent.OtherEvent.Point) continue; //Ignore this negligible length line.
                    result.Add(sweepEvent);
                }
            }

            //Next stage. Find the paths
            var hashResult = new HashSet<SweepEvent>(result);
            var hashPoints = new HashSet<Point>();
            for (var i = 0; i < result.Count; i++)
            {
                result[i].PositionInResult = i;
                result[i].Processed = false;
                if (!hashResult.Contains(result[i].OtherEvent)) throw new Exception("Error in implementation. Both sweep events in the pair should be in this list.");
                var point = result[i].Point;
                if (hashPoints.Contains(point))
                {
                    hashPoints.Remove(point);
                    if (!point.InResult) point.InResult = true;
                    else point.InResultMultipleTimes = true;
                }
                else hashPoints.Add(point);
            }
            if (hashPoints.Select(point => hashPoints.Where(otherPoint => !point.Equals(otherPoint)).Any(otherPoint => point == otherPoint)).Any(duplicateFound => !duplicateFound))
            {
                throw new Exception("Point appears in list an odd number of times. This means there are missing sweep events or one too many.");
            }

            var solution = new Paths();
            var currentPathID = 0;
            Path previousPath = null;
            foreach (var se1 in result.Where(se1 => !se1.Processed))
            {
                int parentID;
                var depth = ComputeDepth(se1, previousPath, out parentID);
                var path = ComputePath(se1, currentPathID, depth, parentID, result);
                if (depth % 2 != 0) //Odd
                {
                    path = CWNegative(path);
                }
                solution.Add(path);
                //if (parent != -1) //parent path ID
                //{
                //    solution[parent].AddChild(currentPathID);
                //}
                currentPathID++;
                previousPath = path;
            }

            return solution;
        }
        #endregion

        #region Set Information
        private static void SetInformation(SweepEvent sweepEvent, SweepEvent previous, BooleanOperationType booleanOperationType, bool isFirstOfDuplicatePair = false)
        {
            //Consider whether the previous edge from the other polygon is an inside-outside transition or outside-inside, based on a vertical ray starting below
            // the previous edge and pointing upwards. //If the transition is outside-inside, the sweepEvent lays inside other polygon, otherwise it lays outside.
            if (previous == null || !previous.LeftToRight)
            {
                //Then it must lie outside the other polygon
                sweepEvent.OtherInOut = false;
                sweepEvent.OtherEvent.OtherInOut = false;
            }
            else //It lies inside the other polygon
            {
                sweepEvent.OtherInOut = true;
                sweepEvent.OtherEvent.OtherInOut = true;
            }

            //Determine if it should be in the results
            if (booleanOperationType == BooleanOperationType.Union)
            {
                //If duplicate (overlapping) edges, the second of the duplicate pair is never kept in the result 
                //The first duplicate pair is in the result if the lines have the same left to right properties.
                //Otherwise, the lines are inside.
                if (sweepEvent.DuplicateEvent != null)
                {
                    if (!isFirstOfDuplicatePair) sweepEvent.InResult = false;
                    else sweepEvent.InResult = sweepEvent.LeftToRight == sweepEvent.DuplicateEvent.LeftToRight;
                }
                else sweepEvent.InResult = !sweepEvent.OtherInOut;
            }
            else if (booleanOperationType == BooleanOperationType.Intersection)
            {
                if (sweepEvent.DuplicateEvent != null) throw new NotImplementedException();
                sweepEvent.InResult = sweepEvent.OtherInOut;
            }
            else throw new NotImplementedException();
        }
        #endregion

        #region Compute Depth
        private static int ComputeDepth(SweepEvent se, Path previousPath, out int parentID)
        {
            //This function shoots a ray down from the sweep event point, which is the first point of the path.
            //it must be the left bottom (min X, then min Y) this sweep event is called se2.
            //Then we use the bool properties of the se2 to determine whether it is inside the polygon that
            //se2 belongs to, its parent, or none.
            if (previousPath != null && se.PrevInResult != null)
            {
                if (se.PrevInResult.LeftToRight)
                {
                    //else, outside-inside transition. It is inside the previous polygon.
                    parentID = se.PrevInResult.PathID;
                    return se.PrevInResult.Depth + 1;
                }
                if (se.PrevInResult.ParentPathID != -1)
                {
                    //It must share the same parent path and depth
                    parentID = se.PrevInResult.ParentPathID;
                    return se.PrevInResult.Depth;
                }
                //else, there is a polygon below it, but it is not inside any polygons
                parentID = -1;
                return 0;
            }
            //else, not inside any other polygons
            parentID = -1;
            return 0;
        }

        #endregion

        #region Compute Paths
        //This can return paths that contain the same point more than once (does this instead of making + and - loop).
        //Could chop them up, but I'm not sure that this is necessary.
        private static List<Point> ComputePath(SweepEvent startEvent, int pathID, int depth, int parentID, IList<SweepEvent> result)
        {
            //First, get the proper start event, given the current guess.
            //The proper start event will be the lowest OtherEvent.Y from neighbors at the startEvent.Point.
            //This will ensure we move CW positive around the path, regardless of previous To/From ordering.
            //Which allows us to handle points with multiple options.
            //We will determine To/From ordering based on the path depth.
            var neighbors = FindAllNeighbors(startEvent, result);
            neighbors.Add(startEvent);
            var yMin = startEvent.OtherEvent.Point.Y;
            var xMin = neighbors.Select(neighbor => neighbor.OtherEvent.Point.X).Concat(new[] { double.PositiveInfinity }).Min();
            foreach (var neighbor in neighbors)
            {
                //We need the lowest neighbor line, not necessarily the lowest Y value.
                if (!neighbor.Left) throw new Exception("First event should always be left.");
                var neighborYIntercept = LineIntercept(neighbor, xMin);
                if (neighborYIntercept < yMin)
                {
                    yMin = neighborYIntercept;
                    startEvent = neighbor;
                }
            }

            var updateAll = new List<SweepEvent> { startEvent };
            var path = new Path();
            startEvent.Processed = false; //This will be to true right at the end of the while loop. 
            var currentSweepEvent = startEvent;
            do
            {
                //Get the other event (endpoint) for this line. 
                currentSweepEvent = currentSweepEvent.OtherEvent;
                currentSweepEvent.Processed = true;
                updateAll.Add(currentSweepEvent);

                //Since result is sorted lexicographically, the event we are looking for will be adjacent to the current sweep event (note that we are staying on the same point)
                neighbors = FindAllNeighbors(currentSweepEvent, result);
                if (!neighbors.Any()) throw new Exception("Must have at least one neighbor");
                if (neighbors.Count == 1)
                {
                    currentSweepEvent = neighbors.First();
                }
                else
                {
                    //Get the minimum signed angle between the two vectors 
                    var minAngle = double.PositiveInfinity;
                    var v1 = currentSweepEvent.Point - currentSweepEvent.OtherEvent.Point;
                    foreach (var neighbor in neighbors)
                    {
                        var v2 = neighbor.OtherEvent.Point - neighbor.Point;
                        var angle = MiscFunctions.InteriorAngleBetweenEdgesInCCWList(v1, v2);
                        if (angle < 0 || angle > 2 * Math.PI) throw new Exception("Error in my assumption of output from above function");
                        if (angle < minAngle)
                        {
                            minAngle = angle;
                            currentSweepEvent = neighbor;
                        }
                    }
                }
                currentSweepEvent.Processed = true;
                updateAll.Add(currentSweepEvent);

                //if (!currentSweepEvent.From) throw new Exception("Error in implementation");
                path.Add(currentSweepEvent.Point); //Add the "From" Point  

            } while (currentSweepEvent != startEvent);

            //Once all the events of the path are found, update their PathID, ParentID, Depth fields
            foreach (var sweepEvent in updateAll)
            {
                sweepEvent.PathID = pathID;
                sweepEvent.Depth = depth;
                sweepEvent.ParentPathID = parentID;

            }

            ////Check if the path should be chopped up with interior paths
            //var alreadyConsidered = new HashSet<Point>();
            //foreach (var point in path.Where(point => point.InResultMultipleTimes))
            //{
            //    if (!alreadyConsidered.Contains(point)) alreadyConsidered.Add(point);
            //    else
            //    {

            //    }
            //}
            return path;
        }

        private static List<SweepEvent> FindAllNeighbors(SweepEvent se1, IList<SweepEvent> result)
        {
            var neighbors = new List<SweepEvent>();
            //Search points upward (incrementing)
            var i = se1.PositionInResult;
            var thisDirection = true;
            do
            {
                i++;
                if (i > result.Count - 1 || se1.Point != result[i].Point) thisDirection = false;
                else if (result[i].Processed) continue;
                else neighbors.Add(result[i]);
            } while (thisDirection);

            //Also check lower decrementing
            i = se1.PositionInResult;
            thisDirection = true;
            do
            {
                i--;
                if (i < 0 || se1.Point != result[i].Point) thisDirection = false;
                else if (result[i].Processed) continue;
                else neighbors.Add(result[i]);
            } while (thisDirection);

            return neighbors;
        }
        #endregion

        #region Check and Resolve Intersection between two lines
        private static void CheckAndResolveIntersection(SweepEvent se1, SweepEvent se2, ref SweepList sweepLines, ref OrderedSweepEventList orderedSweepEvents, out bool goBack)
        {
            goBack = false;
            if (se1 == null || se2 == null) return;
            //if (se1.DuplicateEvent == se2) return;

            var newSweepEvents = new List<SweepEvent>();

            Point intersectionPoint;
            if (MiscFunctions.LineLineIntersection(se1.Point, se1.OtherEvent.Point,
                se2.Point, se2.OtherEvent.Point, out intersectionPoint, true) && intersectionPoint == null)
            {
                #region SPECIAL CASE: Collinear
                //SPECIAL CASE: Collinear
                if (se1.Point == se2.Point)
                {
                    if (se1.OtherEvent.Point.X.IsPracticallySame(se2.OtherEvent.Point.X))
                    {
                        //if (se1.PolygonType == se2.PolygonType) throw new NotImplementedException();
                        //Else set duplicates
                    }
                    else if (se1.OtherEvent.Point.X < se2.OtherEvent.Point.X)
                    {
                        //Order goes (1) se1.Point == se2.Point, (2) se1.OtherEvent.Point, (3) se2.OtherEvent.Point
                        //Segment se2 
                        newSweepEvents.AddRange(Segment(se2, se1.OtherEvent.Point));
                    }
                    else
                    {
                        //Order goes (1) se1.Point == se2.Point, (2) se2.OtherEvent.Point, (3) se1.OtherEvent.Point
                        //Segment se1 
                        newSweepEvents.AddRange(Segment(se1, se2.OtherEvent.Point));
                    }
                    //Set DuplicateEvents
                    se1.DuplicateEvent = se2;
                    se1.OtherEvent.DuplicateEvent = se2.OtherEvent;
                    se2.DuplicateEvent = se1;
                    se2.OtherEvent.DuplicateEvent = se1.OtherEvent;
                }

                else
                {
                    //Reorder if necessary (reduces the amount of code)
                    if (se1.Point.X > se2.Point.X)
                    {
                        var temp = se1;
                        se1 = se2;
                        se2 = temp;
                    }

                    if (se1.OtherEvent.Point == se2.OtherEvent.Point)
                    {
                        //Order goes, (1) se1.Point, (2) se2.Point, (3) se1.OtherEvent.Point == se2.OtherEvent.Point
                        goBack = true;
                        sweepLines.RemoveAt(se2.IndexInList);
                        orderedSweepEvents.Insert(se2);

                        //Segment se1
                        var se1Other = se1.OtherEvent;
                        newSweepEvents.AddRange(Segment(se1, se2.Point));

                        //Set DuplicateEvents
                        se2.DuplicateEvent = newSweepEvents[1];
                        newSweepEvents[1].DuplicateEvent = se2;
                        se1Other.DuplicateEvent = se2.OtherEvent;
                        se2.OtherEvent.DuplicateEvent = se1Other;
                    }
                    else if (se1.OtherEvent.Point.X.IsPracticallySame(se2.OtherEvent.Point.X)) throw new NotImplementedException();
                    else if (se1.OtherEvent.Point.X < se2.OtherEvent.Point.X)
                    {
                        //Order goes, (1) se1.Point, (2) se2.Point, (3) se1.OtherEvent.Point, (4) se2.OtherEvent.Point
                        goBack = true;
                        sweepLines.RemoveAt(se2.IndexInList);
                        orderedSweepEvents.Insert(se2);

                        //Segment se1
                        var se1Other = se1.OtherEvent;
                        newSweepEvents.AddRange(Segment(se1, se2.Point));

                        //Segment se2
                        newSweepEvents.AddRange(Segment(se2, se1Other.Point));

                        //Set DuplicateEvents
                        se2.DuplicateEvent = newSweepEvents[1];
                        newSweepEvents[1].DuplicateEvent = se2;
                        se1Other.DuplicateEvent = se2.OtherEvent;
                        se2.OtherEvent.DuplicateEvent = se1Other;
                    }
                    else
                    {
                        //Order goes, (1) se1.Point, (2) se2.Point, (3) se2.OtherEvent.Point, (4) se1.OtherEvent.Point
                        goBack = true;
                        sweepLines.RemoveAt(se2.IndexInList);
                        orderedSweepEvents.Insert(se2);

                        //Segment se1
                        newSweepEvents.AddRange(Segment(se1, se2.Point));

                        //Segment second new sweep event
                        newSweepEvents.AddRange(Segment(newSweepEvents[1], se2.OtherEvent.Point));

                        //Set DuplicateEvents
                        se2.DuplicateEvent = newSweepEvents[1];
                        newSweepEvents[1].DuplicateEvent = se2;
                        se2.OtherEvent.DuplicateEvent = newSweepEvents[2];
                        newSweepEvents[2].DuplicateEvent = se2.OtherEvent;
                    }
                }

                //Add all new sweep events
                foreach (var sweepEvent in newSweepEvents)
                {
                    orderedSweepEvents.Insert(sweepEvent);
                }
                return;
                #endregion
            }

            //GENERAL CASE: Lines share a point and cannot possibly intersect. It was not collinear, so return.
            if (se1.Point == se2.Point || se1.Point == se2.OtherEvent.Point ||
                se1.OtherEvent.Point == se2.Point || se1.OtherEvent.Point == se2.OtherEvent.Point)
            {
                return;
            }
            //GENERAL CASE: Lines do not intersect.
            if (intersectionPoint == null) return;

            //SPECIAL CASE: Intersection point is the same as one of previousSweepEvent's line end points.
            if (intersectionPoint == se1.Point)
            {
                var se2Other = se2.OtherEvent;

                var newSweepEvent1 = new SweepEvent(se1.Point, false, !se2.From, se2.PolygonType) { OtherEvent = se2 };
                se2.OtherEvent = newSweepEvent1;

                var newSweepEvent2 = new SweepEvent(se1.Point, true, !se2Other.From, se2.PolygonType) { OtherEvent = se2Other };
                se2Other.OtherEvent = newSweepEvent2;

                //Add all new sweep events
                orderedSweepEvents.Insert(newSweepEvent1);
                orderedSweepEvents.Insert(newSweepEvent2);
            }

            else if (intersectionPoint == se1.OtherEvent.Point)
            {
                var se2Other = se2.OtherEvent;

                var newSweepEvent1 = new SweepEvent(se1.OtherEvent.Point, false, !se2.From, se2.PolygonType) { OtherEvent = se2 };
                se2.OtherEvent = newSweepEvent1;

                var newSweepEvent2 = new SweepEvent(se1.OtherEvent.Point, true, !se2Other.From, se2.PolygonType) { OtherEvent = se2Other };
                se2Other.OtherEvent = newSweepEvent2;

                //Add all new sweep events
                orderedSweepEvents.Insert(newSweepEvent1);
                orderedSweepEvents.Insert(newSweepEvent2);
            }

            //SPECIAL CASE: Intersection point is the same as one of se2's line end points. 
            else if (intersectionPoint == se2.Point)
            {
                var se1Other = se1.OtherEvent;

                var newSweepEvent1 = new SweepEvent(se2.Point, false, !se1.From, se1.PolygonType) { OtherEvent = se1 };
                se1.OtherEvent = newSweepEvent1;

                var newSweepEvent2 = new SweepEvent(se2.Point, true, !se1Other.From, se1.PolygonType) { OtherEvent = se1Other };
                se1Other.OtherEvent = newSweepEvent2;

                //Add all new sweep events
                orderedSweepEvents.Insert(newSweepEvent1);
                orderedSweepEvents.Insert(newSweepEvent2);
            }

            else if (intersectionPoint == se2.OtherEvent.Point)
            {
                var se1Other = se1.OtherEvent;

                var newSweepEvent1 = new SweepEvent(se2.OtherEvent.Point, false, !se1.From, se1.PolygonType) { OtherEvent = se1 };
                se1.OtherEvent = newSweepEvent1;

                var newSweepEvent2 = new SweepEvent(se2.OtherEvent.Point, true, !se1Other.From, se1.PolygonType) { OtherEvent = se1Other };
                se1Other.OtherEvent = newSweepEvent2;

                //Add all new sweep events
                orderedSweepEvents.Insert(newSweepEvent1);
                orderedSweepEvents.Insert(newSweepEvent2);
            }

            //GENERAL CASE: Lines are not parallel and only intersct once, between the end points of both lines.
            else
            {
                var se1Other = se1.OtherEvent;
                var se2Other = se2.OtherEvent;

                //Split Sweep Event 1 (previousSweepEvent)
                var newSweepEvent1 = new SweepEvent(intersectionPoint, false, !se1.From, se1.PolygonType) { OtherEvent = se1 };
                se1.OtherEvent = newSweepEvent1;

                var newSweepEvent2 = new SweepEvent(intersectionPoint, true, !se1Other.From, se1.PolygonType) { OtherEvent = se1Other };
                se1Other.OtherEvent = newSweepEvent2;

                //Split Sweep Event 2 (se2)
                var newSweepEvent3 = new SweepEvent(intersectionPoint, false, !se2.From, se2.PolygonType) { OtherEvent = se2 };
                se2.OtherEvent = newSweepEvent3;

                var newSweepEvent4 = new SweepEvent(intersectionPoint, true, !se2Other.From, se2.PolygonType) { OtherEvent = se2Other };
                se2Other.OtherEvent = newSweepEvent4;

                //Add all new sweep events
                orderedSweepEvents.Insert(newSweepEvent1);
                orderedSweepEvents.Insert(newSweepEvent2);
                orderedSweepEvents.Insert(newSweepEvent3);
                orderedSweepEvents.Insert(newSweepEvent4);
            }
        }

        private static IEnumerable<SweepEvent> Segment(SweepEvent sweepEvent, Point point)
        {
            var sweepEventOther = sweepEvent.OtherEvent;
            //Split Sweep Event 1 (previousSweepEvent)
            var newSweepEvent1 = new SweepEvent(point, false, !sweepEvent.From, sweepEvent.PolygonType) { OtherEvent = sweepEvent };
            sweepEvent.OtherEvent = newSweepEvent1;

            var newSweepEvent2 = new SweepEvent(point, true, !sweepEventOther.From, sweepEvent.PolygonType) { OtherEvent = sweepEventOther };
            sweepEventOther.OtherEvent = newSweepEvent2;

            return new List<SweepEvent> { newSweepEvent1, newSweepEvent2 };
        }

        #endregion

        #region SweepList for Boolean Operations
        private class SweepList
        {
            private List<SweepEvent> _sweepEvents;

            public int Count => _sweepEvents.Count;

            public SweepEvent Next(int i)
            {
                if (i == _sweepEvents.Count - 1) return null;
                var sweepEvent = _sweepEvents[i + 1];
                sweepEvent.IndexInList = i + 1;
                return sweepEvent;
            }

            public SweepEvent Previous(int i)
            {
                if (i == 0) return null;
                var sweepEvent = _sweepEvents[i - 1];
                sweepEvent.IndexInList = i - 1;
                return sweepEvent;
            }

            public SweepEvent PreviousOther(int i)
            {
                var current = _sweepEvents[i];
                while (i > 0)
                {
                    i--; //Decrement
                    var previous = _sweepEvents[i];
                    if (current.PolygonType == previous.PolygonType) continue;
                    if (current.Point.Y.IsPracticallySame(previous.Point.Y)) return previous; //The Y's are the same, so use the upper most sweepEvent (earliest in list) to determine if inside.
                    if (current.Point.Y < previous.Point.Y && current.Point.Y < previous.OtherEvent.Point.Y)
                    {
                        //Note that it is possible for either the previous.Point or previous.OtherEvent.Point to be below the current point, as long as the previous point is to the left
                        //of the current.Point and is sloped below or above the current point.
                        throw new Exception("Error in implemenation (sorting?). This should never happen.");
                    }
                    return previous;
                }
                //No other polygon event was found. Return null (or duplicate event if it exists).
                return current.DuplicateEvent;
            }

            public SweepEvent PreviousInResult(int i)
            {
                while (i > 0)
                {
                    i--; //Decrement
                    var previous = _sweepEvents[i];
                    if (!previous.InResult) continue;
                    return previous;
                }
                //No other polygon event was found. Return null.
                return null;
            }

            public void RemoveAt(int i)
            {
                _sweepEvents.RemoveAt(i);
            }

            //Insert, ordered min Y to max Y for the intersection of the line with xval.
            public int Insert(SweepEvent se1)
            {
                if (se1 == null) throw new Exception("Must not be null");
                if (!se1.Left) throw new Exception("Right end point sweep events are not supposed to go into this list");

                if (_sweepEvents == null)
                {
                    _sweepEvents = new List<SweepEvent> { se1 };
                    return 0;
                }

                var se1Y = se1.Point.Y;
                var i = 0;
                foreach (var se2 in _sweepEvents)
                {
                    if (se1.Point == se2.Point)
                    {
                        var m1 = se1.Slope;
                        var m2 = se2.Slope;
                        if (m1.IsPracticallySame(m2) || m1 < m2) //if collinear or se1 is below se2
                        {
                            break; //ok to insert before (Will be marked as a duplicate event)               
                        }
                        //Else increment and continue
                        i++;
                        continue;
                    }

                    var se2Y = LineIntercept(se2.Point, se2.OtherEvent.Point, se1.Point.X);
                    if (se1Y.IsPracticallySame(se2Y)) //se1 intersects se2 with its left endpoint. Both edges are facing to the right.
                    {
                        var m1 = se1.Slope;
                        var m2 = se2.Slope;
                        if (m1.IsPracticallySame(m2) || m1 < m2) //if collinear or se1 is below se2
                        {
                            break; //ok to insert before (Will be marked as a duplicate event)   
                        }
                    }
                    else if (se1Y < se2Y)
                    {
                        break;
                    } //Else increment 
                    i++;
                }
                _sweepEvents.Insert(i, se1);
                return i;
            }


            public int Find(SweepEvent se)
            {
                //ToDo: Could store the position to avoid this time consuming function call
                return _sweepEvents.IndexOf(se);
            }

            public SweepEvent Item(int i)
            {
                return _sweepEvents[i];
            }
        }
        #endregion

        #region SweepEvent and OrderedSweepEventList
        //Sweep Event is used for the boolean operations.
        //We don't want to use lines, because maintaining them (and their referencesis incredibly difficult.
        //There are two sweep events for each line, one for the left edge and one for the right edge
        //Only the sweep events for the left edge are ever added to the sweep list
        private class SweepEvent
        {
            public int IndexInList { get; set; }
            public Point Point { get; } //the point for this sweep event
            public bool Left { get; } //The left endpoint of the line
            public bool From { get; } //The point comes first in the path.
            public SweepEvent OtherEvent { get; set; } //The event of the other endpoint of this line
            public PolygonType PolygonType { get; } //Whether this line was part of the Subject or Clip
            public bool LeftToRight { get; }
            //represents an inside/outside transition in the its polygon tree (Suject or Clip). This occurs when the edge's "Left" has a higher X value.
            public bool OtherInOut { get; set; }
            //represents an inside/outside transition in the other polygon tree (Suject or Clip). This occurs when the edge's "Left" has a higher X value.
            public bool InResult { get; set; }
            //A bool to track which sweep events are part of the result (set depending on boolean operation).
            public SweepEvent PrevInResult { get; set; }
            //A pointer to the closest ende downwards in S that belongs to the result polgyon. Used to calculate depth and parentIDs.
            public int PositionInResult { get; set; }
            //public bool ResultInsideOut { get; set; } //The field ResultInsideOut is set to true if the right endpoint sweep event precedes the left endpoint sweepevent in the path.
            public int PathID { get; set; }
            public int ParentPathID { get; set; }
            public bool Processed { get; set; } //If this sweep event has already been processed in the sweep
            public int Depth { get; set; }
            public SweepEvent DuplicateEvent { get; set; }


            public SweepEvent(Point point, bool isLeft, bool isFrom, PolygonType polyType)
            {
                Point = point;
                Left = isLeft;
                From = isFrom;
                PolygonType = polyType;
                LeftToRight = From == Left; //If both left and from, or both right and To, then LeftToRight = true;
                DuplicateEvent = null;
                _slope = new Lazy<double>(GetSlope);
            }

            //Slope as a lazy property, since it is not required for all sweep events
            private readonly Lazy<double> _slope;
            public double Slope => _slope.Value;

            private double GetSlope()
            {
                //Solve for slope and y intercept. 
                if (Point.X.IsPracticallySame(OtherEvent.Point.X)) //If vertical line, set slope = inf.
                {
                    return double.MaxValue;
                }
                if (Point.Y.IsPracticallySame(OtherEvent.Point.Y)) //If horizontal line, set slope = 0.
                {
                    return 0.0;
                }
                //Else y = mx + Yintercept
                return (OtherEvent.Point.Y - Point.Y) / (OtherEvent.Point.X - Point.X);
            }
        }

        private class OrderedSweepEventList
        {
            private readonly List<SweepEvent> _sweepEvents;

            public OrderedSweepEventList(IEnumerable<SweepEvent> sweepEvents)
            {
                _sweepEvents = new List<SweepEvent>();
                foreach (var sweepEvent in sweepEvents)
                {
                    Insert(sweepEvent);
                }
            }

            public void Insert(SweepEvent se1)
            {
                //Find the index for p1
                var i = 0;
                var breakIfNotNear = false;
                SweepEvent previousSweepEvent = null;
                foreach (var se2 in _sweepEvents)
                {
                    if (se1.Point == se2.Point) //reference is the same
                    {
                        if (se1.OtherEvent.Point.X.IsPracticallySame(se2.OtherEvent.Point.X))
                        {
                            //If the slopes are practically collinear (the point is the same, so we only need partial slopes (assume pt 1 = 0,0)
                            var m1 = se1.OtherEvent.Point.Y / se1.OtherEvent.Point.X;
                            var m2 = se2.OtherEvent.Point.Y / se2.OtherEvent.Point.X;
                            if (se1.OtherEvent.Point.Y.IsPracticallySame(se2.OtherEvent.Point.Y) || m1.IsPracticallySame(m2))
                            {
                                if (previousSweepEvent == null || previousSweepEvent.PolygonType == se2.PolygonType)
                                {
                                    break; //ok to insert before (Will be marked as a duplicate event)
                                    //If the previousSweepEvent and se2 have the same polygon type, insert se1 before se2.
                                    //This is to help with determining the result using the previous other line.
                                }
                                //Else increment and continue;                         
                            }
                            else if (se1.OtherEvent.Point.Y < se2.OtherEvent.Point.Y)
                            {
                                //Insert before se2
                                break;
                            }   //Else increment and continue;
                        }
                        //If both left endpoints, add whichever line is lower than the other
                        //To determine this, use whichever X is more left to determine a Y intercept for the other line at that x value.
                        //If the calculated Y is > or = to the OtherPoint.Y then it is considered above line with the lower x value..
                        else if (se1.Left && se2.Left)
                        {
                            double se1Y, se2Y;
                            if (!se1.OtherEvent.Point.X.IsGreaterThanNonNegligible(se2.OtherEvent.Point.X)) // <= is equivalent to !GreaterThanNonNegligible(value)
                            {
                                se1Y = se1.OtherEvent.Point.Y;
                                se2Y = LineIntercept(se2.Point, se2.OtherEvent.Point, se1.OtherEvent.Point.X);
                            }
                            else
                            {
                                se1Y = LineIntercept(se1.Point, se1.OtherEvent.Point, se2.OtherEvent.Point.X);
                                se2Y = se2.OtherEvent.Point.Y;
                            }
                            if (se1Y < se2Y)
                            {
                                //Insert before se2
                                break;
                            }   //Else increment and continue;
                        }
                        else if (se1.OtherEvent.Point.X < se2.OtherEvent.Point.X)
                        {
                            //Insert before se2
                            break;
                        }   //Else increment and continue;
                    }
                    else if (se1.Point.X.IsPracticallySame(se2.Point.X))
                    {
                        //if (se1.Point.Y.IsPracticallySame(se2.Point.Y)) throw new NotImplementedException("Sweep Events need to be merged"); 
                        if (se1.Point.Y < se2.Point.Y) break;
                        breakIfNotNear = true;
                    }
                    else if (breakIfNotNear) break;
                    else if (se1.Point.X < se2.Point.X) break;
                    i++;
                    previousSweepEvent = se2;
                }
                _sweepEvents.Insert(i, se1);
            }
            public bool Any()
            {
                return _sweepEvents.Any();
            }

            public SweepEvent First()
            {
                return _sweepEvents.First();
            }

            public void RemoveAt(int i)
            {
                _sweepEvents.RemoveAt(i);
            }
        }
        #endregion

        #region Other Various Private Functions: PreviousInResult, LineIntercept, & IsPointOnSegment
        private static SweepEvent PreviousInResult(SweepEvent se1, IList<SweepEvent> result)
        {
            //Get the first sweep event that goes below previousSweepEvent
            var i = se1.PositionInResult;
            var se1Y = se1.Point.Y;
            while (i > 0)
            {
                i--; //Decrement
                var se2 = result[i];
                var se2Y = LineIntercept(se2.Point, se2.OtherEvent.Point, se1.Point.X);
                var tempPoint = new Point(se1.Point.X, se2Y);
                if (se2Y < se1Y && IsPointOnSegment(se2.Point, se2.OtherEvent.Point, tempPoint))
                {
                    return se2;
                }
            }
            return null;
        }

        private static double LineIntercept(SweepEvent se, double xval)
        {
            return LineIntercept(se.Point, se.OtherEvent.Point, xval);
        }

        private static double LineIntercept(Point p1, Point p2, double xval)
        {
            if (p1.X.IsPracticallySame(p2.X)) //Vertical line
            {
                //return lower value Y
                return p1.Y < p2.Y ? p1.Y : p2.Y;
            }
            if (p1.Y.IsPracticallySame(p2.Y))//Horizontal Line
            {
                return p1.Y;
            }
            //Else, find the slope and then solve for y
            var m = (p2.Y - p1.Y) / (p2.X - p1.X);
            return m * (xval - p1.X) + p1.Y;
        }

        private static bool IsPointOnSegment(Point p1, Point p2, Point pointInQuestion)
        {
            if ((pointInQuestion.X < p1.X && pointInQuestion.X < p2.X) ||
                (pointInQuestion.X > p1.X && pointInQuestion.X > p2.X) ||
                (pointInQuestion.Y < p1.Y && pointInQuestion.Y < p2.Y) ||
                (pointInQuestion.Y > p1.Y && pointInQuestion.Y > p2.Y)) return false;
            return true;
        }
        #endregion

        //Mirrors a shape along a given direction, such that the mid line is the same for both the original and mirror
        public static PolygonsAsLight Mirror(PolygonsAsLight shape, double[] direction2D)
        {
            var mirror = new PolygonsAsLight();
            var points = new List<PointLight>();
            foreach (var path in shape)
            {
                foreach (var point in path.Path)
                {
                    points.Add(point);
                }
            }
            MinimumEnclosure.GetLengthAndExtremePoints(direction2D, points, out var bottomPoints, out _);
            var distanceFromOriginToClosestPoint = bottomPoints[0].dotProduct(direction2D);
            foreach (var polygon in shape)
            {
                var newPath = new PathAsLight();
                foreach (var point in polygon.Path)
                {
                    //Get the distance to the point along direction2D
                    //Then subtract 2X the distance along direction2D
                    var d = point.dotProduct(direction2D) - distanceFromOriginToClosestPoint;
                    newPath.Add(new PointLight(point.X - direction2D[0] * 2 * d, point.Y - direction2D[1] * 2 * d));
                }
                //Reverse the new path so that it retains the same CW/CCW direction of the original
                newPath.Reverse();
                mirror.Add(new PolygonLight(newPath));
                if (!mirror.Last().Area.IsPracticallySame(polygon.Area, Constants.BaseTolerance))
                {
                    throw new Exception("Areas do not match after mirroring the polygons");
                }
            }
            return mirror;
        }
    }
}
