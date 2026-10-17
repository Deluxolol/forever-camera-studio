namespace Machinae.Forever
{
    public static class PlayerPointerCodec
    {
        private static ulong Ror(ulong value, int bits) { return (value >> bits) | (value << (64-bits)); }
        public static ulong Decode70450(ulong encoded, ulong key)
        {
            unchecked {
                ulong value=Ror(Ror(encoded,57)^0x059636330EFB6366UL,9);
                value+=0x1E3090C1B3F708C4UL;
                value^=0x81UL;
                value+=0x50C4C7A61E914956UL;
                value^=key;
                return value-0x5BCD232CC1C81EE5UL;
            }
        }
        public static ulong Decode70374(ulong encoded, ulong key)
        {
            unchecked {
                ulong value = (encoded + 0xD6150C8C3C4BAE0CUL) ^ 0x66C1D13E29C28326UL;
                value += 0xDCC41FE97852543AUL;
                value = Ror(value,3);
                value ^= 0x5FUL;
                value -= 0x226B38657EBDEF29UL;
                value ^= key;
                return Ror(value,59);
            }
        }
    }
}
