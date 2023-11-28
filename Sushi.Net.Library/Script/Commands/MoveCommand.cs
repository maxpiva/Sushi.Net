namespace Sushi.Net.Library.Script
{
    public class CopyCommand : BaseCommand
    {
        public float MoveTime { get; set; }
        public override float OrderDuration => Duration;
    }
}