using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Sushi.Net.Library.Tools
{
    public interface IPercentageProcessor
    {
        public void Init();
        public int PercentageFromLine(string line);

    }
}
