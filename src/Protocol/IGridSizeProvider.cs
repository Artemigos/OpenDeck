namespace OpenDeck.Protocol
{
    public interface IGridSizeProvider
    {
        (uint width, uint height) GetGridSize();
    }
}
