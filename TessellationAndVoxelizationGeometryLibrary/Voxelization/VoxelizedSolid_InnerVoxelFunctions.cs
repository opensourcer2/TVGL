﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 02-27-2015
//
// Last Modified By : Matt Campbell
// Last Modified On : 09-21-2017
// ***********************************************************************
// <copyright file="VoxelizedSolid.cs" company="Design Engineering Lab">
//     Copyright ©  2017
// </copyright>
// <summary></summary>
// ***********************************************************************

using StarMathLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TVGL.Voxelization
{
    /// <summary>
    /// Class VoxelizedSolid.
    /// </summary>
    public partial class VoxelizedSolid
    {
        #region Functions to make voxel full, empty or partial

        private IVoxel MakeVoxelFull(IVoxel voxel)
        {
            if (voxel.Role == VoxelRoleTypes.Full) return voxel;
            if (voxel.Level == 0)
            {
                if (voxel.Role == VoxelRoleTypes.Empty) //then you may need to add it
                {
                    if (!(voxel is Voxel_Level0_Class))
                        voxel = new Voxel_Level0_Class(voxel.ID, VoxelRoleTypes.Full, this);
                    lock (voxelDictionaryLevel0)
                        if (!voxelDictionaryLevel0.Contains(voxel.ID))
                            voxelDictionaryLevel0.Add(voxel);
                }
                else
                {
                    lock (((Voxel_Level0_Class)voxel).InnerVoxels)
                        ((Voxel_Level0_Class)voxel).InnerVoxels = null;
                    foreach (var nextLevelVoxel in ((Voxel_Level0_Class)voxel).InnerVoxels[0])
                        lock (voxelDictionaryLevel1)
                            voxelDictionaryLevel1.Remove(nextLevelVoxel);
                    lock (((Voxel_Level0_Class)voxel).NextLevelVoxels)
                        ((Voxel_Level0_Class)voxel).NextLevelVoxels.Clear();
                }

                ((Voxel_Level0_Class)voxel).Role = VoxelRoleTypes.Full;
            }
            else if (voxel.Level == 1)
            {
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                var id0 = Constants.MakeContainingVoxelID(thisIDwoFlags, 0);
                if (!voxelDictionaryLevel0.ContainsKey(id0)) MakeVoxelPartial(new Voxel(id0, 0));
                var voxel0 = voxelDictionaryLevel0[id0];
                lock (voxel0)
                {
                    if (voxel.Role == VoxelRoleTypes.Empty)
                    {
                        if (!(voxel is Voxel_Level1_Class))
                            voxel = new Voxel_Level1_Class(voxel.ID, VoxelRoleTypes.Full, this);
                        lock (voxelDictionaryLevel1)
                            if (!voxelDictionaryLevel1.ContainsKey(voxel.ID))
                            {
                                voxelDictionaryLevel1.Add(voxel.ID, (Voxel_Level1_Class)voxel);
                                voxel0.NextLevelVoxels.Add(voxel.ID);
                            }
                    }
                    else //then it was partial
                        voxel0.InnerVoxels.RemoveWhere(id =>
                            Constants.MakeContainingVoxelID(id, 1) == thisIDwoFlags);

                    ((Voxel_Level1_Class)voxel).Role = VoxelRoleTypes.Full;
                    if (voxel0.NextLevelVoxels.Count == 4096
                        && voxel0.NextLevelVoxels.All(v => voxelDictionaryLevel1[v].Role == VoxelRoleTypes.Full))
                        MakeVoxelFull(voxel0);
                }
            }
            else if (voxel.Level == 2)
            {
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                voxel = new Voxel(thisIDwoFlags + Constants.SetRoleFlags(2, VoxelRoleTypes.Full), this);
                var id0 = Constants.MakeContainingVoxelID(thisIDwoFlags, 0);
                if (!voxelDictionaryLevel0.ContainsKey(id0)) MakeVoxelPartial(new Voxel(id0, 0));
                var voxel0 = voxelDictionaryLevel0[id0];
                if (voxel0.Role == VoxelRoleTypes.Full) return voxel;

                var id1 = Constants.MakeContainingVoxelID(thisIDwoFlags, 1);
                if (!voxelDictionaryLevel1.ContainsKey(id1)) MakeVoxelPartial(new Voxel(id1, 1));
                var voxel1 = voxelDictionaryLevel1[id0];
                if (voxel1.Role == VoxelRoleTypes.Full) return voxel;
                var makeParentFull = false;
                lock (voxel0.InnerVoxels)
                {
                    voxel0.InnerVoxels.Remove(voxel.ID);
                    if (voxel.Role == VoxelRoleTypes.Partial)
                        voxel0.InnerVoxels.RemoveWhere(id => Constants.MakeContainingVoxelID(id, 2) == thisIDwoFlags);
                    voxel0.InnerVoxels.Add(voxel.ID);
                    makeParentFull = (voxel0.InnerVoxels.Count(isFullLevel2) == 4096);
                }
                if (makeParentFull) MakeVoxelFull(GetParentVoxel(voxel));
            }
            else if (voxel.Level == 3)
            {
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                voxel = new Voxel(thisIDwoFlags + Constants.SetRoleFlags(3, VoxelRoleTypes.Full), this);
                var id0 = Constants.MakeContainingVoxelID(thisIDwoFlags, 0);
                if (!voxelDictionaryLevel0.ContainsKey(id0)) MakeVoxelPartial(new Voxel(id0, 0));
                var voxel0 = voxelDictionaryLevel0[id0];
                if (voxel0.Role == VoxelRoleTypes.Full) return voxel;

                var id1 = Constants.MakeContainingVoxelID(thisIDwoFlags, 1);
                if (!voxelDictionaryLevel1.ContainsKey(id1)) MakeVoxelPartial(new Voxel(id1, 1));
                var voxel1 = voxelDictionaryLevel1[id0];
                if (voxel1.Role == VoxelRoleTypes.Full) return voxel;

                var id2 = Constants.MakeContainingVoxelID(thisIDwoFlags, 2);
                if (!isFullLevel2(voxel0.InnerVoxels.GetFullVoxelID(id2))) return voxel;
                if (!isPartialLevel2(voxel0.InnerVoxels.GetFullVoxelID(id2)))
                    MakeVoxelPartial(new Voxel(id2, 2));
                var makeParentFull = false;
                lock (voxel0.InnerVoxels)
                {
                    voxel0.InnerVoxels.Remove(voxel.ID);
                    if (voxel.Role == VoxelRoleTypes.Partial)
                        voxel0.InnerVoxels.RemoveWhere(id => Constants.MakeContainingVoxelID(id, 3) == thisIDwoFlags);
                    voxel0.InnerVoxels.Add(voxel.ID);
                    makeParentFull = (GetSiblingVoxels(voxel).Count(v => isFullLevel3(v.ID)) == 4096);
                }
                if (makeParentFull) MakeVoxelFull(GetParentVoxel(voxel));
            }
            else //if (voxel.Level == 4)
            {
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                voxel = new Voxel(thisIDwoFlags + Constants.SetRoleFlags(4, VoxelRoleTypes.Full), this);
                var id0 = Constants.MakeContainingVoxelID(thisIDwoFlags, 0);
                if (!voxelDictionaryLevel0.ContainsKey(id0)) MakeVoxelPartial(new Voxel(id0, 0));
                var voxel0 = voxelDictionaryLevel0[id0];
                if (voxel0.Role == VoxelRoleTypes.Full) return voxel;

                var id1 = Constants.MakeContainingVoxelID(thisIDwoFlags, 1);
                if (!voxelDictionaryLevel1.ContainsKey(id1)) MakeVoxelPartial(new Voxel(id1, 1));
                var voxel1 = voxelDictionaryLevel1[id0];
                if (voxel1.Role == VoxelRoleTypes.Full) return voxel;

                var id2 = Constants.MakeContainingVoxelID(thisIDwoFlags, 2);
                if (!isFullLevel2(voxel0.InnerVoxels.GetFullVoxelID(id2))) return voxel;
                if (!isPartialLevel2(voxel0.InnerVoxels.GetFullVoxelID(id2)))
                    MakeVoxelPartial(new Voxel(id2, 2));

                var id3 = Constants.MakeContainingVoxelID(thisIDwoFlags, 3);
                if (!isFullLevel3(voxel0.InnerVoxels.GetFullVoxelID(id3))) return voxel;
                if (!isPartialLevel3(voxel0.InnerVoxels.GetFullVoxelID(id3)))
                    MakeVoxelPartial(new Voxel(id3, 3));

                var makeParentFull = false;
                lock (voxel0.InnerVoxels)
                {
                    voxel0.InnerVoxels.Remove(voxel.ID);
                    voxel0.InnerVoxels.Add(voxel.ID);
                    makeParentFull = (GetSiblingVoxels(voxel).Count(v => isFullLevel4(v.ID)) == 4096);
                }
                if (makeParentFull) MakeVoxelFull(GetParentVoxel(voxel));
            }
            return voxel;
        }

        private void MakeVoxelEmpty(IVoxel voxel)
        {
            if (voxel.Role == VoxelRoleTypes.Empty) return;
            if (voxel.Level == 0)
            {
                if (voxel.Role == VoxelRoleTypes.Partial)
                    lock (voxelDictionaryLevel1)
                        foreach (var nextLevelVoxel in ((Voxel_Level0_Class)voxel).NextLevelVoxels)
                            voxelDictionaryLevel1.Remove(nextLevelVoxel);
                // no need to do anything with the HigherLevelVoxels as deleting this Voxel, deletes
                // the only reference to these higher voxels.
                lock (voxelDictionaryLevel0)
                    voxelDictionaryLevel0.Remove(voxel.ID);
            }
            else if (voxel.Level == 1)
            {
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID, false);
                var voxel0 = voxelDictionaryLevel0[Constants.MakeContainingVoxelID(thisIDwoFlags, 0)];
                lock (voxel0.NextLevelVoxels)
                    voxel0.NextLevelVoxels.Remove(thisIDwoFlags);
                if (voxel0.NextLevelVoxels.Count == 0) MakeVoxelEmpty(voxel0);
                if (voxel.Role == VoxelRoleTypes.Partial)
                    lock (voxel0.InnerVoxels)
                        voxel0.InnerVoxels.RemoveWhere(id => Constants.MakeContainingVoxelID(id, 1) == thisIDwoFlags);
                lock (voxelDictionaryLevel1) voxelDictionaryLevel1.Remove(voxel.ID);
            }
            else
            {
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                var voxel0 = voxelDictionaryLevel0[Constants.MakeContainingVoxelID(thisIDwoFlags, 0)];
                lock (voxel0.InnerVoxels)
                    voxel0.InnerVoxels.Remove(voxel.ID);
                if (!GetSiblingVoxels(voxel).Any()) MakeVoxelEmpty(GetParentVoxel(voxel));
                if (voxel.Role == VoxelRoleTypes.Partial)
                {
                    if (voxel.Level == 2)
                        lock (voxel0.InnerVoxels)
                            voxel0.InnerVoxels.RemoveWhere(
                                id => Constants.MakeContainingVoxelID(id, 2) == thisIDwoFlags);
                    else if (voxel.Level == 3)
                        lock (voxel0.InnerVoxels)
                            voxel0.InnerVoxels.RemoveWhere(id => Constants.MakeContainingVoxelID(id, 3) == thisIDwoFlags);
                }
            }
            // return voxel;
        }


        private IVoxel MakeVoxelPartial(IVoxel voxel)
        {
            if (voxel.Role == VoxelRoleTypes.Partial) return voxel;
            if (voxel.Level == 0)
            {
                if (!(voxel is Voxel_Level0_Class))
                    voxel = new Voxel_Level0_Class(voxel.ID, VoxelRoleTypes.Empty, this);
                lock (voxelDictionaryLevel0)
                    if (!voxelDictionaryLevel0.ContainsKey(voxel.ID))
                        voxelDictionaryLevel0.Add(voxel.ID, (Voxel_Level0_Class)voxel);
                ((Voxel_Level0_Class)voxel).NextLevelVoxels = new VoxelHashSet(new VoxelComparerCoarse(), this);
                ((Voxel_Level0_Class)voxel).InnerVoxels = new VoxelHashSet(new VoxelComparerFine(), this);
                if (voxel.Role == VoxelRoleTypes.Full)
                    for (int i = 0; i < 16; i++)
                        for (int j = 0; j < 16; j++)
                            for (int k = 0; k < 16; k++)
                            {
                                var subVoxel = new Voxel_Level1_Class(voxel.ID + (i * 65536L) +
                                                                      (j * 68719476736) +
                                                                      (k * 72057594037927936L),
                                    VoxelRoleTypes.Full, this);
                                lock (voxelDictionaryLevel1)
                                    voxelDictionaryLevel1.Add(subVoxel.ID, (Voxel_Level1_Class)subVoxel);
                                ((Voxel_Level0_Class)voxel).NextLevelVoxels.Add(subVoxel.ID);
                            }
                ((Voxel_Level0_Class)voxel).Role = VoxelRoleTypes.Partial;
            }
            else if (voxel.Level == 1)
            {
                bool wasFull = voxel.Role == VoxelRoleTypes.Full;
                Voxel_Level0_Class voxel0;
                var id0 = Constants.MakeContainingVoxelID(voxel.ID, 0);
                lock (voxelDictionaryLevel0)
                {
                    if (!voxelDictionaryLevel0.ContainsKey(id0))
                        MakeVoxelPartial(new Voxel(id0, 0));
                    voxel0 = voxelDictionaryLevel0[id0];
                }

                if (voxel0.Role == VoxelRoleTypes.Full) voxel0 = (Voxel_Level0_Class)MakeVoxelPartial(voxel0);
                if (!(voxel is Voxel_Level1_Class))
                    voxel = new Voxel_Level1_Class(voxel.ID, VoxelRoleTypes.Partial, this);
                lock (voxelDictionaryLevel1)
                    if (!voxelDictionaryLevel1.ContainsKey(voxel.ID))
                    {
                        voxelDictionaryLevel1.Add(voxel.ID, (Voxel_Level1_Class)voxel);
                        voxel0.NextLevelVoxels.Add(voxel.ID);
                    }

                ((Voxel_Level1_Class)voxel).Role = VoxelRoleTypes.Partial;
                if (wasFull && discretizationLevel > 1)
                {
                    var id2 = Constants.ClearFlagsFromID(voxel.ID, false)
                              + Constants.SetRoleFlags(2, VoxelRoleTypes.Full);
                    for (int i = 0; i < 16; i++)
                        for (int j = 0; j < 16; j++)
                            for (int k = 0; k < 16; k++)
                                voxel0.InnerVoxels.Add(id2 + (i * 4096) +
                                                           (j * 4294967296L) +
                                                           (k * 4503599627370496));
                }
            }
            else if (voxel.Level == 2)
            {
                bool wasFull = voxel.Role == VoxelRoleTypes.Full;
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                voxel = new Voxel(thisIDwoFlags + Constants.SetRoleFlags(2, VoxelRoleTypes.Partial), this);
                var id0 = Constants.MakeContainingVoxelID(thisIDwoFlags, 0);
                if (!voxelDictionaryLevel0.ContainsKey(id0)) MakeVoxelPartial(new Voxel(id0, 0));
                var voxel0 = voxelDictionaryLevel0[id0];
                if (voxel0.Role == VoxelRoleTypes.Full) voxel0 = (Voxel_Level0_Class)MakeVoxelPartial(voxel0);

                var id1 = Constants.MakeContainingVoxelID(thisIDwoFlags, 1);
                if (!voxelDictionaryLevel1.ContainsKey(id1)) MakeVoxelPartial(new Voxel(id1, 1));
                var voxel1 = voxelDictionaryLevel1[id0];
                if (voxel1.Role == VoxelRoleTypes.Full) MakeVoxelPartial(voxel1);
                lock (voxel0.InnerVoxels)
                {
                    voxel0.InnerVoxels.Remove(voxel.ID);
                    voxel0.InnerVoxels.Add(voxel.ID);
                }
                if (wasFull && discretizationLevel > 2)
                {
                    var id3 = Constants.ClearFlagsFromID(voxel.ID, true)
                              + Constants.SetRoleFlags(2, VoxelRoleTypes.Full);
                    for (int i = 0; i < 16; i++)
                        for (int j = 0; j < 16; j++)
                            for (int k = 0; k < 16; k++)
                                voxel0.InnerVoxels.Add(id3 + (i * 256) +
                                                           (j * 268435456) +
                                                           (k * 281474976710656));
                }
            }
            else if (voxel.Level == 3)
            {
                bool wasFull = voxel.Role == VoxelRoleTypes.Full;
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                voxel = new Voxel(thisIDwoFlags + Constants.SetRoleFlags(3, VoxelRoleTypes.Partial), this);
                var id0 = Constants.MakeContainingVoxelID(thisIDwoFlags, 0);
                if (!voxelDictionaryLevel0.ContainsKey(id0)) MakeVoxelPartial(new Voxel(id0, 0));
                var voxel0 = voxelDictionaryLevel0[id0];
                if (voxel0.Role == VoxelRoleTypes.Full) voxel0 = (Voxel_Level0_Class)MakeVoxelPartial(voxel0);

                var id1 = Constants.MakeContainingVoxelID(thisIDwoFlags, 1);
                if (!voxelDictionaryLevel1.ContainsKey(id1)) MakeVoxelPartial(new Voxel(id1, 1));
                var voxel1 = voxelDictionaryLevel1[id0];
                if (voxel1.Role == VoxelRoleTypes.Full) MakeVoxelPartial(voxel1);

                var id2 = Constants.MakeContainingVoxelID(thisIDwoFlags, 2);
                if (!isPartialLevel2(voxel0.InnerVoxels.GetFullVoxelID(id2)))
                    MakeVoxelPartial(new Voxel(id2, 2));
                lock (voxel0.InnerVoxels)
                {
                    voxel0.InnerVoxels.Remove(voxel.ID);
                    voxel0.InnerVoxels.Add(voxel.ID);
                }
                if (wasFull && discretizationLevel > 3)
                {
                    var id4 = Constants.ClearFlagsFromID(voxel.ID, true)
                              + Constants.SetRoleFlags(2, VoxelRoleTypes.Full);
                    for (int i = 0; i < 16; i++)
                        for (int j = 0; j < 16; j++)
                            for (int k = 0; k < 16; k++)
                                voxel0.InnerVoxels.Add(id4 + (i * 16) +
                                                           (j * 16777216) +
                                                           (k * 17592186044416));
                }
            }
            else //if (voxel.Level == 4)
            {
                var thisIDwoFlags = Constants.ClearFlagsFromID(voxel.ID);
                voxel = new Voxel(thisIDwoFlags + Constants.SetRoleFlags(4, VoxelRoleTypes.Partial), this);
                var id0 = Constants.MakeContainingVoxelID(thisIDwoFlags, 0);
                if (!voxelDictionaryLevel0.ContainsKey(id0)) MakeVoxelPartial(new Voxel(id0, 0));
                var voxel0 = voxelDictionaryLevel0[id0];
                if (voxel0.Role == VoxelRoleTypes.Full) voxel0 = (Voxel_Level0_Class)MakeVoxelPartial(voxel0);

                var id1 = Constants.MakeContainingVoxelID(thisIDwoFlags, 1);
                if (!voxelDictionaryLevel1.ContainsKey(id1)) MakeVoxelPartial(new Voxel(id1, 1));
                var voxel1 = voxelDictionaryLevel1[id0];
                if (voxel1.Role == VoxelRoleTypes.Full) MakeVoxelPartial(voxel1);

                var id2 = Constants.MakeContainingVoxelID(thisIDwoFlags, 2);
                if (!isPartialLevel2(voxel0.InnerVoxels.GetFullVoxelID(id2)))
                    MakeVoxelPartial(new Voxel(id2, 2));

                var id3 = Constants.MakeContainingVoxelID(thisIDwoFlags, 3);
                if (!isPartialLevel3(voxel0.InnerVoxels.GetFullVoxelID(id3)))
                    MakeVoxelPartial(new Voxel(id3, 3));
                lock (voxel0.InnerVoxels)
                {
                    voxel0.InnerVoxels.Remove(voxel.ID);
                    voxel0.InnerVoxels.Add(voxel.ID);
                }
            }
            return voxel;
        }

        #endregion

        private void UpdateProperties(int level = -1)
        {
            _totals = new[]
                        {
                            voxelDictionaryLevel0.Count(v => v.Role == VoxelRoleTypes.Full),
                            voxelDictionaryLevel0.Count(v => v.Role == VoxelRoleTypes.Partial),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,1,VoxelRoleTypes.Full)),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,1,VoxelRoleTypes.Partial)),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,2,VoxelRoleTypes.Full)),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,2,VoxelRoleTypes.Partial)),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,3,VoxelRoleTypes.Full)),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,4,VoxelRoleTypes.Partial)),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,4,VoxelRoleTypes.Full)),
                            voxelDictionaryLevel0.Sum(dict => CountVoxels(dict,4,VoxelRoleTypes.Partial))
                        };
            Volume = 0.0;
            for (int i = 0; i <= discretizationLevel; i++)
                Volume += Math.Pow(VoxelSideLengths[i], 3) * _totals[2 * i];
            Volume += Math.Pow(VoxelSideLengths[discretizationLevel], 3) * _totals[2 * discretizationLevel + 1];
            _count = _totals.Sum();
        }

        private long CountVoxels(IVoxel dict, int level, VoxelRoleTypes role)
        {
            var innerVoxels =((Voxel_Level0_Class)dict).InnerVoxels;
            if (innerVoxels == null || innerVoxels.Length < level) return 0L;
            return innerVoxels[level - 1].Count(v => v.Role == role);
        }

        internal double[] GetRealCoordinates(long ID, int level)
        {
            return GetRealCoordinates(level, Constants.GetCoordinateIndices(ID, level));
            //var indices = Constants.GetCoordinateIndices(ID, level);
            //return indices.multiply(VoxelSideLengths[level]).add(Offset);
        }

        internal double[] GetRealCoordinates(int level, params int[] indices)
        {
            return indices.multiply(VoxelSideLengths[level]).add(Offset);
        }

        #region Quick Booleans for IDs
        internal static bool isFull(long ID)
        {
            return (ID & 3) == 3;
        }
        internal static bool isEmpty(long ID)
        {
            return (ID & 3) == 0;
        }
        internal static bool isPartial(long ID)
        {
            var id = ID & 3;
            return id == 1 || id == 2;
        }

        internal static bool isLevel4(long ID)
        {
            return (ID & 15) >= 12;
        }
        internal static bool isLevel3(long ID)
        {
            var id = ID & 15;
            return id < 12 && id >= 8;
        }
        internal static bool isLevel2(long ID)
        {
            var id = ID & 15;
            return id < 8 && id >= 4;
        }
        internal static bool isLevel1(long ID)
        {
            var id = ID & 31;
            return id < 20 && id >= 16;
        }
        internal static bool isLevel0(long ID)
        {
            var id = ID & 31;
            return id < 4;
        }
        internal static bool isFullLevel2(long ID)
        {
            return isLevel2(ID) && isFull(ID);
        }
        internal static bool isPartialLevel2(long ID)
        {
            return isLevel2(ID) && isPartial(ID);
        }
        internal static bool isFullLevel3(long ID)
        {
            return isLevel3(ID) && isFull(ID);
        }
        internal static bool isPartialLevel3(long ID)
        {
            return isLevel3(ID) && isPartial(ID);
        }
        internal static bool isFullLevel4(long ID)
        {
            return isLevel4(ID) && isFull(ID);
        }
        internal static bool isPartialLevel4(long ID)
        {
            return isLevel4(ID) && isPartial(ID);
        }
        #endregion
    }
}