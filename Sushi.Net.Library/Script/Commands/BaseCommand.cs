namespace Sushi.Net.Library.Script
{
    public abstract class BaseCommand
    {
        public float Time { get; set; }
        public float Duration { get; set;}
        public abstract float OrderDuration { get;} 
    }
}