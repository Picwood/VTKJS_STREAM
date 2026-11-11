// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.Collections.Generic;
using System.Linq;
using CaeGlobals;

namespace CaeMesh
{
    public static class MeshGeometryIds
    {
        private const int _partDigits = 10000;
        private const int _typeDigits = 100000;
        public static int[] GetPartIdsFromGeometryIds(int[] geometryIds)
        {
            if (geometryIds == null) return null;
            //
            HashSet<int> partIds = new HashSet<int>();
            int[] itemTypePartIds;
            for (int i = 0; i < geometryIds.Length; i++)
            {
                itemTypePartIds = GetItemTypePartIdsFromGeometryId(geometryIds[i]);
                partIds.Add(itemTypePartIds[2]);
            }
            //
            return partIds.ToArray();
        }
        public static int GetPartIdFromGeometryId(int geometryId)
        {
            return GetItemTypePartIdsFromGeometryId(geometryId)[2];
        }
        public static int[] GetItemTypePartIdsFromGeometryId(int geometryId)
        {
            // geometryId = itemId * 100000 + typeId * 10000 + partId;
            int partId = geometryId % _partDigits;
            int typeId = (geometryId / _partDigits) % 10;     // GeometryType
            int itemId = geometryId / _typeDigits;
            return new int[] { itemId, typeId, partId };
        }
        public static int GetTypeIdFromGeometryId(int geometryId)
        {
            // geometryId = itemId * 100000 + typeId * 10000 + partId;
            //int partId = geometryId % _partDigits;
            int typeId = (geometryId / 10000) % 10;     // GeometryType
            //int itemId = geometryId / _typeDigits;
            return typeId;
        }
        public static int GetItemIdFromGeometryId(int geometryId)
        {
            // geometryId = itemId * 100000 + typeId * 10000 + partId;
            //int partId = geometryId % _partDigits;
            //int typeId = (geometryId / 10000) % 10;     // GeometryType
            int itemId = geometryId / _typeDigits;
            return itemId;
        }
        public static int GetGeometryPartIdFromGeometryId(int geometryId)
        {
            int[] itemTypePartIds = GetItemTypePartIdsFromGeometryId(geometryId);
            return GetGeometryId(0, (int)GeometryType.Part, itemTypePartIds[2]);
        }
        public static int GetGeometryId(int itemId, int typeId, int partId)
        {
            return itemId * _typeDigits + typeId * _partDigits + partId;
        }
    }
}
