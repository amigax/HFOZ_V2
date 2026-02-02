// Taken from my other package, Dots Plus.
// For similar helper functions, see https://assetstore.unity.com/packages/tools/utilities/dots-plus-227492
//namespace ProjectDawn.Collections
namespace ProjectDawn.Navigation
{
    public interface IUnion<T> where T : unmanaged
    {
        /// <summary>
        /// Returns combined volume.
        /// </summary>
        T Union(T value);
    }
}
