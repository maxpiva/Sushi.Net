using System.Collections.Generic;
using System.Text;

namespace Sushi.Net.Library.Events.Subtitles.Aegis;

public class GarbageList : List<string>
{
    public override string ToString()
    {
        StringBuilder bld = new();
        bld.AppendLine("[Aegisub Project Garbage]");
        ForEach(a => bld.AppendLine(a));
        return bld.ToString();
    }

}