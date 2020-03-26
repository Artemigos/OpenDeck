using System;

namespace OpenDeck.Protocol
{
    public interface IButtonEventSource
    {
        event EventHandler<ButtonEventArgs> ButtonDown;
        event EventHandler<ButtonEventArgs> ButtonUp;
        event EventHandler<ButtonEventArgs> ButtonClick;
    }
}
