namespace ServerApplication.Framing.VariableSized.LengthFielded
{
    internal static class Helper
    {
        // Size of the Header. In this case 2 * int -> 2 * 4.
        public static int HeaderLength => 8;
    }
}
