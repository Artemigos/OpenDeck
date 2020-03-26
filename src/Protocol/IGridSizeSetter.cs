namespace OpenDeck.Protocol
{
    public interface IGridSizeSetter
    {
        (uint width, uint height) MinGridSize { get; }

        (uint width, uint height) MaxGridSize { get; }

        void SetGridSize(uint width, uint height);
    }
}
