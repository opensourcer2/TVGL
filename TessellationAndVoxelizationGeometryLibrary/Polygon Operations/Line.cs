﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TVGL.Numerics;


namespace TVGL
{
    /// <summary>
    /// A 2D Line made up of two points.
    /// </summary>
    public class Line
    {

        #region Public Properties
        /// <summary>
        ///     Gets the Pointwhich the line is pointing to. Set is through the constructor.
        /// </summary>
        /// <value>To node.</value>
        public Point ToPoint { get; private set; }

        /// <summary>
        ///     Gets the Pointwhich the line is pointing to. Set is through the constructor.
        /// </summary>
        /// <value>To node.</value>
        public Vector2 Center => new Vector2((ToPoint.X + FromPoint.X) / 2, (ToPoint.Y + FromPoint.Y) / 2);

        /// <summary>
        ///     Gets the Pointwhich the line is pointing away from. Set is through the constructor.
        /// </summary>
        /// <value>From node.</value>
        public Point FromPoint { get; private set; }

        /// <summary>
        ///     Gets the Slope.
        /// </summary>
        /// <value>The Slope.</value>
        public double Slope { get; private set; }

        /// <summary>
        /// Gets whether line is horizontal
        /// </summary>
        public bool IsHorizontal { get; private set; }

        /// <summary>
        /// Gets whether line is vertical
        /// </summary>
        public bool IsVertical { get; private set; }

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        // ReSharper disable once InconsistentNaming
        /// <summary>
        ///     Gets the Yintercept.
        /// </summary>
        /// <value>The Yintercept.</value>
        public double Yintercept { get; private set; }

        /// <summary>
        /// Get or set its index in a list.
        /// </summary>
        public int IndexInPath { get; set; }

        /// <summary>
        /// Gets the length of the line
        /// </summary>
        public double Length { get; private set; }

        /// <summary>
        ///     Gets or sets an arbitrary ReferenceIndex to track this line
        /// </summary>
        /// <value>The reference index.</value>
        public int ReferenceIndex { get; set; }

        // ReSharper disable once InconsistentNaming
        public double dY; //Rise

        // ReSharper disable once InconsistentNaming
        public double dX; //Run

        /// <summary>
        /// If the line is infinite, it only has a FromPoint and Slope. ToPoint is null
        /// </summary>
        public bool IsInfinite = false;

        public Vector2 LineVector;
        #endregion

        #region Constructor

        /// <summary>
        ///     Sets to and from points as well as slope and intercept of line.
        /// </summary>
        /// <param name="fromPoint"></param>
        /// <param name="lineVector"></param>
        internal Line(Point fromPoint, Vector2 lineVector)
        {
            //This line has an infinite length
            IsInfinite = true;
            FromPoint = fromPoint;
            ToPoint = null;
            LineVector = lineVector;
            dX = lineVector.X;
            dY = lineVector.Y;
        }

        /// <summary>
        ///     Sets to and from points as well as slope and intercept of line.
        /// </summary>
        /// <param name="fromPoint"></param>
        /// <param name="toPoint"></param>
        /// <param name="twoWayReference"></param>
        internal Line(Point fromPoint, Point toPoint, bool twoWayReference = true)
        {
            FromPoint = fromPoint;
            ToPoint = toPoint;
            Length = FromPoint.Light.Distance(ToPoint.Light);
            IsHorizontal = false;
            IsVertical = false;
            dY = ToPoint.Y - FromPoint.Y;
            dX = ToPoint.X - FromPoint.X;
            LineVector = new Vector2(dX, dY );

            //Solve for slope and y intercept. 
            if (ToPoint.X.IsPracticallySame(FromPoint.X)) //If vertical line, set slope = inf.
            {
                Slope = double.MaxValue;
                //use maxvalue instead of infinity, since IsPracticallyTheSame comparison does not work with infinity
                Yintercept = double.MaxValue;
                //use maxvalue instead of infinity, since IsPracticallyTheSame comparison does not work with infinity
                IsVertical = true;
            }

            else if (ToPoint.Y.IsPracticallySame(FromPoint.Y)) //If horizontal line, set slope = 0.
            {
                Slope = 0.0;
                IsHorizontal = true;
                Yintercept = ToPoint.Y;
            }
            else //Else y = mx + Yintercept
            {
                Slope = (dY) / (dX);
                Yintercept = ToPoint.Y - Slope * ToPoint.X;
            }

            if (!twoWayReference) return;
            FromPoint.Lines.Add(this);
            ToPoint.Lines.Add(this);
        }

        internal Line(Line line)
        {
            FromPoint = line.FromPoint;
            ToPoint = line.ToPoint;
            dY = line.dY;
            dX = line.dX;
            LineVector = line.LineVector;
            Length = line.Length;
            IsHorizontal = line.IsHorizontal;
            IsVertical = line.IsVertical;
            Slope = line.Slope;
            Yintercept = line.Yintercept;
        }
        #endregion

        #region Public Methods        
        /// <summary>
        ///     Reverses this line.
        /// </summary>
        public void Reverse()
        {
            var tempPoint = FromPoint;
            FromPoint = ToPoint;
            ToPoint = tempPoint;
        }

        /// <summary>
        /// Gets the other point that makes up this line.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Point OtherPoint(Point point)
        {
            if (point == FromPoint) return ToPoint;
            return point == ToPoint ? FromPoint : null;
        }

        /// <summary>
        /// Returns Y value given an X value
        /// </summary>
        /// <param name="xval"></param>
        /// <returns></returns>
        public double YGivenX(double xval)
        {
            if (IsHorizontal)
            {
                //Any y value on the line will do
                return FromPoint.Y;
            }
            if (IsVertical)
            {
                //return either positive or negative infinity depending on the direction of the line.
                if (ToPoint.Y - FromPoint.Y > 0)
                {
                    return double.MaxValue;
                }
                return double.MinValue;
            }
            return Slope * xval + Yintercept;
        }

        /// <summary>
        /// Returns X value given a Y value
        /// </summary>
        /// <param name="y">The y.</param>
        /// <returns>System.Double.</returns>
        public double XGivenY(double y)
        {
            //If a vertical line, return an x value on that line (e.g., ToNode.X)
            if (IsVertical)
            {
                return FromPoint.X;
            }

            //If a flat line give either positive or negative infinity depending on the direction of the line.
            if (IsHorizontal)
            {
                if (ToPoint.X - FromPoint.X > 0)
                {
                    return double.MaxValue;
                }
                return double.MinValue;
            }
            return (y - Yintercept) / Slope;
        }
        #endregion
    }
}

