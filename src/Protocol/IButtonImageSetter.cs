namespace OpenDeck.Protocol
{
    public interface IButtonImageSetter
    {
        (uint width, uint height) PreferredResolution { get; }

        void SetButtonImage(uint x, uint y, byte[] image, Image.Types.Format format, uint imageWidth, uint imageHeight);
    }
}
