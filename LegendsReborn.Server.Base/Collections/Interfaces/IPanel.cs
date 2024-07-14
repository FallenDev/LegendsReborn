using Darkages.Enums;

namespace Darkages.Collections.Interfaces;

public interface IPanel<T>
{
    bool IsFull { get; }
    Pane PaneType { get; }
    T this[byte slot] { get; }
    bool IsValidSlot(byte slot);
    bool Contains(T obj);
    bool TryGetObject(byte slot, out T obj);
    bool TryGetRemove(byte slot, out T obj);
    bool TryAdd(T obj, byte slot);
    bool TryAddToNextSlot(T obj);
    bool Remove(byte slot);
    bool TrySwap(byte slot1, byte slot2);
    IEnumerable<T> Snapshot(Func<T, bool> predicate = null);
    void Assert(Action<T[]> action);
    TResult Assert<TResult>(Func<T[], TResult> func);
    void Update(byte slot, Action<T> action);
    void Save();
}