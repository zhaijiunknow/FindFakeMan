namespace Project.Core.Runtime.Framework
{
    public interface ISaveable<T>
    {
        T GetSaveData();
        void LoadState(T data);
    }
}
