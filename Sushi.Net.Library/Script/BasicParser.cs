using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sushi.Net.Library.Script
{
    public abstract class BasicParser
    {
        public abstract Task<bool> ProcessAsync(string[] command);

        public abstract List<string> Serialize(bool absolute=false);    

        public LineCounter Lines { get; }

        public BasicParser(LineCounter counter)
        {
            Lines=counter;
        }
        public BasicParser()
        {
            Lines = new LineCounter(new List<string>());
        }

        public async Task ProcessAsync()
        {
            do
            {
                string[] split = Lines.GetNextSplited();
                if (split == null)
                {
                    return;
                }

                bool ret=await ProcessAsync(split).ConfigureAwait(false);
                if (ret)
                {
                    return;
                }
            } while (true);

        }


    }
}