
using System.Collections.Generic;

namespace RealSenseData
{
    class MyBlobs
    {
        public int numBlobs { get; set; }
        public List<List<PXCMPointI32>> blobs { get; set; }
        public List<PXCMPoint3DF32> closestPoints { get; set; }
    }
}