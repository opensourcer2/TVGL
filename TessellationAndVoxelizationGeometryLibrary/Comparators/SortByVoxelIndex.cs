using System;
using System.Collections.Generic;
using TVGL.Voxelization;

namespace TVGL
{
    internal class SortByVoxelIndex : IComparer<VoxelWithTessellationLinks>
    {
        private int dimension;
        private int sense;

        internal SortByVoxelIndex(int dimension)
        {
            sense = Math.Sign(dimension);
            this.dimension = Math.Abs(dimension) - 1;
        }
        public int Compare(VoxelWithTessellationLinks x, VoxelWithTessellationLinks y)
        {
            if (x.CoordinateIndices[dimension] > y.CoordinateIndices[dimension]) return sense;
            else return -sense;
        }
    }
    internal class SortByVoxelID : IComparer<long>
    {
        private int dimension;
        private int sense;

        internal SortByVoxelID(VoxelDirections direction)
        {
            sense = Math.Sign((int)direction);
            this.dimension = Math.Abs((int)direction) - 1;
        }
        public int Compare(long x, long y)
        {
            switch (dimension)
            {
                case 0:
                    if ((x & Voxelization.Constants.maskAllButX) > (y & Voxelization.Constants.maskAllButX))
                        return sense;
                    else return -sense;
                case 1:
                    if ((x & Voxelization.Constants.maskAllButY) > (y & Voxelization.Constants.maskAllButY))
                        return sense;
                    else return -sense;
                default:
                    if ((x & Voxelization.Constants.maskAllButZ) > (y & Voxelization.Constants.maskAllButZ))
                        return sense;
                    else return -sense;
            }
        }
    }
}