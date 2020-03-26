namespace OpenDeck.Protocol
{
    public interface IButtonLabelSetter
    {
        uint MaxLength { get; }

        void SetButtonLabel(uint x, uint y, string label);
    }
}
