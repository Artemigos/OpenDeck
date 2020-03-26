namespace OpenDeck.Protocol
{
    public interface IButtonImageSetter
    {
        (uint width, uint height) PreferredResolution { get; }

        void SetButtonImage(uint x, uint y, byte[] image, uint imageWidth, uint imageHeight);
    }
}
