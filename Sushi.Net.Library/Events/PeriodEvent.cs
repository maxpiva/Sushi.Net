using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sushi.Net.Library.Events
{
    public class PeriodEvent : Event
    {
        public override bool IsComment => false;
        public override string FormatTime(float seconds)
        {
            return string.Empty;
        }

        public override Event Clone()
        {
            return new PeriodEvent
            {
                End = this.End,
                Start = this.Start,
                SourceIndex = this.SourceIndex,
                Text = this.Text
            };
        }
    }
}
