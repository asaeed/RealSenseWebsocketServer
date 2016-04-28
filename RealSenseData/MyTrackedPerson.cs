//--------------------------------------------------------------------------------------
// Copyright 2016 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.
//--------------------------------------------------------------------------------------
namespace RealSenseData
{
    class MyTrackedPerson
    {
        public int PersonsDetected { get; set; }
        public int H { get; set; }
        public int W { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public int FacesDetected { get; set; }
        public int FaceH { get; set; }
        public int FaceW { get; set; }
        public int FaceX { get; set; }
        public int FaceY { get; set; }
        public float FaceDepth { get; set; }
    }
}
