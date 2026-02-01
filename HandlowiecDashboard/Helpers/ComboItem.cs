namespace Kalendarz1.HandlowiecDashboard.Helpers
{
    public class ComboItem
    {
        public int Value { get; set; }
        public string Text { get; set; }
        public override string ToString() => Text;
    }
}
