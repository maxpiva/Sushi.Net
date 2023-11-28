namespace Sushi.Net.Library.Events.Audio
{
    public class ComputedMovement
    {
        public float RelativePosition { get; set; }
        public float Difference { get; set; }
        public float AbsolutePosition { get; set; }
        public int Warning { get; set; }

    }
}