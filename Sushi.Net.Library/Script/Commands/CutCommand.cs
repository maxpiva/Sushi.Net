namespace Sushi.Net.Library.Script
{
    public class CutCommand : BaseCommand
    {
        public override float OrderDuration => -Duration;
    }
}