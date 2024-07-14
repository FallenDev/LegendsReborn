namespace Darkages.Collections.Interfaces;

public interface IDisplay<in T>
{
    void Display(T obj);
    void Remove(byte slot);
}