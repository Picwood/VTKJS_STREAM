// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;

namespace CaeMesh
{
    public static class MeshTopology
    {
        public static int[][] GetVisualizationEdgeCells(int[] cell, ElementFaceType elementFaceType)
        {
            //int[] lookUp3 = new int[] { 0, 1, 2, 0 };
            //int[] lookUp4 = new int[] { 0, 1, 2, 3, 0 };
            //int[] lookUp6 = new int[] { 0, 3, 1, 4, 2, 5, 0 };
            //int[] lookUp8 = new int[] { 0, 4, 1, 5, 2, 6, 3, 7, 0 };

            if (elementFaceType == ElementFaceType.Edge)
            {
                if (cell.Length == 2) return new int[][] { new int[] { cell[0], cell[1] } };
                else if (cell.Length == 3) return new int[][] { new int[] { cell[0], cell[1], cell[2] } };
                else throw new NotSupportedException();
            }
            else
            {
                if (cell.Length == 3)
                {
                    return new int[][] { new int[] { cell[0], cell[1] },
                                         new int[] { cell[1], cell[2] },
                                         new int[] { cell[2], cell[0] } };
                }
                else if (cell.Length == 4)
                {
                    return new int[][] { new int[] { cell[0], cell[1] },
                                         new int[] { cell[1], cell[2] },
                                         new int[] { cell[2], cell[3] },
                                         new int[] { cell[3], cell[0] } };
                }
                else if (cell.Length == 6)
                {
                    return new int[][] { new int[] { cell[0], cell[1], cell[3] },
                                         new int[] { cell[1], cell[2], cell[4] },
                                         new int[] { cell[2], cell[0], cell[5] } };

                    //return new int[][] {    new int[] { cell[0], cell[3], cell[1] },
                    //                        new int[] { cell[1], cell[4], cell[2] },
                    //                        new int[] { cell[2], cell[5], cell[0] } };
                }
                else if (cell.Length == 8)
                {
                    return new int[][] { new int[] { cell[0], cell[1], cell[4] },
                                         new int[] { cell[1], cell[2], cell[5] },
                                         new int[] { cell[2], cell[3], cell[6] },
                                         new int[] { cell[3], cell[0], cell[7] } };

                    //return new int[][] {    new int[] { cell[0], cell[4], cell[1] },
                    //                        new int[] { cell[1], cell[5], cell[2] },
                    //                        new int[] { cell[2], cell[6], cell[3] },
                    //                        new int[] { cell[3], cell[7], cell[0] } };
                }
                else throw new NotSupportedException();
            }
        }
    }
}
