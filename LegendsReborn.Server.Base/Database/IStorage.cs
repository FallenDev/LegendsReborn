namespace Darkages.Database;

public interface IStorage<T>
{
    T Load(string name);
    T LoadAisling(string name);
    bool Save<TA>(T obj);
}