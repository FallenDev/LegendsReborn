using Darkages.Collections.Interfaces;
using Darkages.Enums;

namespace Darkages.Collections.Abstractions;

public abstract class DisplayedStoredPanel<T> : IPanel<T> where T: class
{
    protected byte[] Invalid { get; }
    protected byte Length { get; }
    protected T[] Objects { get; }
    protected IBetterStorage<T> Storage { get; }
    protected IDisplay<T> Display { get; }
    protected object Sync { get; } = new();
    protected int TotalSlots { get; }

    public T this[byte slot] => TryGetObject(slot, out var obj) ? obj : null;

    public bool IsFull
    {
        get
        {
            lock (Sync)
                return Objects.Count(obj => obj != null) >= TotalSlots;
        }
    }

    public Pane PaneType { get; }

    protected DisplayedStoredPanel(
        Pane paneType,
        byte length,
        byte[] invalidSlots,
        IBetterStorage<T> storage = null,
        IDisplay<T> display = null)
    {
        PaneType = paneType;
        Length = length;
        Invalid = invalidSlots;
        Storage = storage;
        Display = display;
        
        

        Objects = new T[Length];
        TotalSlots = Length - Invalid.Length;
    }

    public virtual void Assert(Action<T[]> action)
    {
        lock (Sync)
            action(Objects);
    }

    public virtual TResult Assert<TResult>(Func<T[], TResult> func)
    {
        lock (Sync)
            return func(Objects);
    }

    public virtual bool Contains(T obj)
    {
        lock (Sync)
            return Objects.Contains(obj);
    }

    public virtual bool IsValidSlot(byte slot) => (slot > 0 && slot < 60) && !Invalid.Contains(slot) && (slot <= Length);

    public virtual bool TryAddToNextSlot(T obj)
    {
        lock (Sync)
        {
            for (byte i = 1; i < Length; i++)
                if ((Objects[i] == null) && IsValidSlot(i))
                    return TryAdd(obj, i);

            return false;
        }
    }

    public virtual bool Remove(byte slot)
    {
        if (!IsValidSlot(slot))
            return false;

        lock (Sync)
        {
            var obj = Objects[slot];

            if (obj == null)
                return false;

            Objects[slot] = null;
            Storage.Remove(obj);
            Display.Remove(slot);

            return true;
        }
    }

    public virtual IEnumerable<T> Snapshot(Func<T, bool> predicate = null)
    {
        List<T> snapshot;

        lock (Sync)
            snapshot = Objects.ToList();

        using var enumerator = snapshot.GetEnumerator();
        byte index = 0;

        while (enumerator.MoveNext())
        {
            if ((enumerator.Current != null)
                && IsValidSlot(index)
                && (predicate?.Invoke(enumerator.Current) ?? true))
                yield return enumerator.Current;

            index++;
        }
    }

    public abstract bool TryAdd(T obj, byte slot);

    public virtual bool TryGetObject(byte slot, out T obj)
    {
        obj = null;

        if (!IsValidSlot(slot))
            return false;

        lock (Sync)
        {
            obj = Objects[slot];

            return obj != null;
        }
    }

    public virtual bool TryGetRemove(byte slot, out T obj)
    {
        obj = null;

        if (!IsValidSlot(slot))
            return false;

        lock (Sync)
        {
            obj = Objects[slot];
            Objects[slot] = null;
            Storage.Remove(obj);
            Display.Remove(slot);

            return obj != null;
        }
    }

    public abstract bool TrySwap(byte slot1, byte slot2);

    public void Update(byte slot, Action<T> action)
    {
        lock (Sync)
        {
            var obj = Objects[slot];

            if (obj != null)
            {
                action(obj);
                Storage.Update(obj);
                Display.Display(obj);
            }
        }
    }

    public void Save() => Storage.Save();
}