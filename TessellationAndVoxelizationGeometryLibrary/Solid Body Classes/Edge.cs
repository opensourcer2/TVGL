﻿using System;
using StarMathLib;

namespace TVGL
{
    /// <summary>
    ///     The straight-line edge class. It connects to two nodes and lies between two faces.
    /// </summary>
    public class Edge
    {
        #region Constructor
        /// <summary>
        ///     Initializes a new instance of the <see cref="Edge" /> class.
        /// </summary>
        /// <param name="fromVertex">From vertex.</param>
        /// <param name="toVertex">To vertex.</param>
        /// <param name="ownedFace">The face.</param>
        /// <param name="otherFace">The other face.</param>
        /// <param name="doublyLinkedVertices"></param>
        /// <param name="edgeReference"></param>
        public Edge(Vertex fromVertex, Vertex toVertex, PolygonalFace ownedFace, PolygonalFace otherFace,
            bool doublyLinkedVertices = true, long edgeReference = 0)
        {
            From = fromVertex;
            To = toVertex;
            EdgeReference = edgeReference;
            _ownedFace = ownedFace;
            _otherFace = otherFace;
            if (ownedFace != null) ownedFace.Edges.Add(this);
            if (otherFace != null) otherFace.Edges.Add(this);
            if (doublyLinkedVertices)
            {
                fromVertex.Edges.Add(this);
                toVertex.Edges.Add(this);
            }
            Vector = new[]
            {
                (To.Position[0] - From.Position[0]),
                (To.Position[1] - From.Position[1]),
                (To.Position[2] - From.Position[2])
            };
            Length = Math.Sqrt(Vector[0] * Vector[0] + Vector[1] * Vector[1] + Vector[2] * Vector[2]);
            //if (Length.IsNegligible(Constants.BaseTolerance)) throw new Exception();
            if (OwnedFace == null || OtherFace == null) return; //No need for the next few functions
            DefineInternalEdgeAngle();
            if (double.IsNaN(InternalAngle)) throw new Exception();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Edge" /> class.
        /// </summary>
        /// <param name="fromVertex">From vertex.</param>
        /// <param name="toVertex">To vertex.</param>
        /// <param name="doublyLinkedVertices"></param>
        public Edge(Vertex fromVertex, Vertex toVertex, bool doublyLinkedVertices)
        {
            From = fromVertex;
            To = toVertex;
            if (doublyLinkedVertices)
            {
                fromVertex.Edges.Add(this);
                toVertex.Edges.Add(this);
            }
            Vector = new[]
            {
                (To.Position[0] - From.Position[0]),
                (To.Position[1] - From.Position[1]),
                (To.Position[2] - From.Position[2])
            };
            Length = Math.Sqrt(Vector[0] * Vector[0] + Vector[1] * Vector[1] + Vector[2] * Vector[2]);
            //if (Length.IsNegligible(Constants.BaseTolerance)) throw new Exception();
            //Since there are no faces yet, internal angle is not calculated.
        }

        #endregion

        private Edge()
        {
        }
        /// <summary>
        ///     Others the vertex.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>Vertex.</returns>
        /// <exception cref="System.Exception">OtherVertex: Vertex thought to connect to edge, but it doesn't.</exception>
        public Vertex OtherVertex(Vertex v)
        {
            if (v == To) return From;
            if (v == From) return To;
            throw new Exception("OtherVertex: Vertex thought to connect to edge, but it doesn't.");
        }

        #region Properties

        /// <summary>
        ///     Gets the From Vertex.
        /// </summary>
        /// <value>
        ///     From.
        /// </value>
        public Vertex From { get; internal set; }

        /// <summary>
        ///     Gets the To Vertex.
        /// </summary>
        /// <value>
        ///     To.
        /// </value>
        public Vertex To { get; internal set; }

        /// <summary>
        ///     Gets the length.
        /// </summary>
        /// <value>
        ///     The length.
        /// </value>
        public double Length { get; internal set; }

        /// <summary>
        ///     Gets the vector.
        /// </summary>
        /// <value>
        ///     The vector.
        /// </value>
        public double[] Vector { get; internal set; }

        private PolygonalFace _otherFace;
        private PolygonalFace _ownedFace;

        /// <summary>
        ///     Gets edge reference (checksum) value, which equals
        ///     "From.IndexInList" + "To.IndexInList" (think strings)
        /// </summary>
        /// <value>
        ///     To.
        /// </value>
        internal long EdgeReference { get; set; }

        /// <summary>
        ///     Gets the owned face (the face in which the from-to direction makes sense
        ///     - that is, produces the proper cross-product normal).
        /// </summary>
        /// <value>
        ///     The owned face.
        /// </value>
        public PolygonalFace OwnedFace
        {
            get { return _ownedFace; }
            internal set
            {
                if (_ownedFace == value) return;
                _ownedFace = value;
                DefineInternalEdgeAngle();
            }
        }

        /// <summary>
        ///     Gets the other face (the face in which the from-to direction doesn not
        ///     make sense- that is, produces the negative cross-product normal).
        /// </summary>
        /// <value>
        ///     The other face.
        /// </value>
        public PolygonalFace OtherFace
        {
            get { return _otherFace; }
            internal set
            {
                if (_otherFace == value) return;
                _otherFace = value;
                DefineInternalEdgeAngle();
            }
        }

        /// <summary>
        ///     Gets the internal angle in radians.
        /// </summary>
        /// <value>
        ///     The internal angle.
        /// </value>
        public double InternalAngle { get; private set; }

        /// <summary>
        ///     Gets the curvature of the surface.
        /// </summary>
        /// <value>
        ///     The curvature of the surface.
        /// </value>
        public CurvatureType Curvature { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether [is part of the convex hull].
        /// </summary>
        /// <value>
        ///     <c>true</c> if [is part of the convex hull]; otherwise, <c>false</c>.
        /// </value>
        public bool PartofConvexHull { get; internal set; }
        /// <summary>
        /// Gets the index in list.
        /// </summary>
        /// <value>The index in list.</value>
        public int IndexInList { get; internal set; }

        /// <summary>
        /// Updates the edge vector and length, if a vertex has been moved.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Update()
        {
            //Reset the vector, since vertices may have been moved.
            Vector = new[]
            {
                (To.Position[0] - From.Position[0]),
                (To.Position[1] - From.Position[1]),
                (To.Position[2] - From.Position[2])
            };
            Length =
                Math.Sqrt(Vector[0] * Vector[0] + Vector[1] * Vector[1] + Vector[2] * Vector[2]);
            DefineInternalEdgeAngle();
            // if (double.IsNaN(InternalAngle)) throw new Exception();
        }
        
        /// <summary>
        ///     Defines the edge angle.
        /// </summary>
        private void DefineInternalEdgeAngle()
        {
            /* this is a tricky function. What we need to do is take the dot-product of the normals.
             * which will give the cos(theta). Calling inverse cosine will result in a value from 0 to
             * pi, but is the edge convex or concave? It is convex if the crossproduct of the normals is 
             * in the same direction as the edge vector (dot product is positive). But we need to know 
             * which face-normal goes first in the cross product calculation as this will change the 
             * resulting direction. The one to go first is the one that "owns" the edge. What I mean by
             * own is that the from-to of the edge makes sense in the counter-clockwise prediction of 
             * the face normal. For one face the from-to will be incorrect (normal facing inwards) - 
             * in some geometry approaches this is solved by the concept of half-edges. Here we will 
             * just re-order the two faces referenced in the edge so that the first is the one that 
             * owns the edge...the face for which the direction makes sense, and the second face will 
             * need to reverse the edge vector to make it work out in a proper counter-clockwise loop 
             * for that face. */
            if (_ownedFace == _otherFace || _ownedFace == null || _otherFace == null)
            {
                InternalAngle = double.NaN;
                Curvature = CurvatureType.Undefined;
                return;
            }
            // is this edge truly owned by the ownedFace? if not reverse
            var faceToIndex = _ownedFace.Vertices.IndexOf(To);
            var faceNextIndex = (faceToIndex + 1 == _ownedFace.Vertices.Count) ? 0 : faceToIndex + 1;
            var nextFaceVertex = _ownedFace.Vertices[faceNextIndex];
            var nextEdgeVector = nextFaceVertex.Position.subtract(To.Position);

            if (Vector.crossProduct(nextEdgeVector).dotProduct(_ownedFace.Normal) < 0)
            {
                /* then switch owned face and opposite face since the predicted normal
                 * is in the wrong direction. When OwnedFace and OppositeFace were defined
                 * it was arbitrary anyway - so this is another by-product of this method - 
                 * correct the owned and opposite faces. */
                var temp = From;
                From = To;
                To = temp;
                Vector = Vector.multiply(-1);
            }
            // it would be messed up if both faces thought they owned this edge. If this is the 
            // case, return the edge has no angle.
            faceToIndex = _otherFace.Vertices.IndexOf(To);
            faceNextIndex = (faceToIndex + 1 == _otherFace.Vertices.Count) ? 0 : faceToIndex + 1;
            nextFaceVertex = _otherFace.Vertices[faceNextIndex];
            nextEdgeVector = nextFaceVertex.Position.subtract(To.Position);
            if (Vector.crossProduct(nextEdgeVector).dotProduct(_ownedFace.Normal) > 0)
            // both faces appear to own the edge...must be something wrong
            {
                InternalAngle = double.NaN;
                Curvature = CurvatureType.Undefined;
                return;
            }
            var dot = _ownedFace.Normal.dotProduct(_otherFace.Normal, 3);
            if (dot > 1.0 || dot.IsPracticallySame(1.0, Constants.BaseTolerance))
            {
                InternalAngle = Math.PI;
                Curvature = CurvatureType.SaddleOrFlat;
            }
            else if (dot < -1.0 || dot.IsPracticallySame(-1.0, Constants.BaseTolerance))
            {
                // is it a crack or a sharp edge?
                // in order to find out we look to the other two faces connected to each
                // face to find out
                var ownedNeighborAvgNormals = new double[3];
                var numNeighbors = 0;
                foreach (var face in _ownedFace.AdjacentFaces)
                {
                    if (face != null && face != _otherFace)
                    {
                        ownedNeighborAvgNormals = ownedNeighborAvgNormals.add(face.Normal);
                        numNeighbors++;
                    }
                }
                ownedNeighborAvgNormals = ownedNeighborAvgNormals.divide(numNeighbors);
                var otherNeighborAvgNormals = new double[3];
                numNeighbors = 0;
                foreach (var face in _otherFace.AdjacentFaces)
                {
                    if (face != null && face != _ownedFace)
                    {
                        otherNeighborAvgNormals = otherNeighborAvgNormals.add(face.Normal);
                        numNeighbors++;
                    }
                }
                otherNeighborAvgNormals = otherNeighborAvgNormals.divide(numNeighbors);
                if (ownedNeighborAvgNormals.crossProduct(otherNeighborAvgNormals).dotProduct(Vector) < 0)
                {
                    InternalAngle = 2 * Math.PI;
                    Curvature = CurvatureType.Concave;
                }
                else
                {
                    InternalAngle = 0.0;
                    Curvature = CurvatureType.Convex;
                }
            }
            else
            {
                var cross = _ownedFace.Normal.crossProduct(_otherFace.Normal).dotProduct(Vector);
                if (cross < 0)
                {
                    InternalAngle = Math.PI + Math.Acos(dot);
                    Curvature = CurvatureType.Concave;
                }
                else //(cross > 0)
                {
                    InternalAngle = Math.PI - Math.Acos(dot);
                    Curvature = CurvatureType.Convex;
                }
            }
        }
        #endregion
    }
}