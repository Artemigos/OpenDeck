using System;

namespace OpenDeck.Protocol
{
    public class ButtonEventArgs : EventArgs
    {
        public ButtonEventArgs(uint x, uint y)
        {
            X = x;
            Y = y;
        }

        public uint X { get; }

        public uint Y { get; }
    }
}
