namespace CoreDump
{
    // The four buttons parts fire on. Everything on the same button fires together.
    public enum ButtonSlot { None, LeftMouse, RightMouse, Space, Shift }

    public static class ButtonSlotExtensions
    {
        public static string Label(this ButtonSlot slot) => slot switch
        {
            ButtonSlot.LeftMouse => "Left mouse",
            ButtonSlot.RightMouse => "Right mouse",
            ButtonSlot.Space => "Space",
            ButtonSlot.Shift => "Shift",
            _ => "no button",
        };

        public static ButtonSlot Next(this ButtonSlot slot) => slot == ButtonSlot.Shift ? ButtonSlot.None : slot + 1;
    }
}
