using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Sushi.Net.Library.Common
{
    public interface IProgressLogger
    {
        Progress<int> CreateProgress();
    }

}