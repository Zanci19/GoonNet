using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GoonNet;

public abstract class DatabaseBase<T> where T : class
{
    protected List<T> _items = new();
    protected string _filePath = string.Empty;
    protected string _lockFilePath = string.Empty;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public DatabaseState State { get; protected set; } = DatabaseState.Idle;

    public event EventHandler? OnLoaded;
    public event EventHandler? OnSaved;
    public event EventHandler? OnLocked;
    public event EventHandler? OnUnlocked;

    protected abstract Guid GetId(T item);
    protected abstract XmlSerializer CreateSerializer();

    public void Initialize(string filePath)
    {
        _filePath = filePath;
        _lockFilePath = filePath + ".lock";
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public async Task LoadAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            State = DatabaseState.Loading;
            if (File.Exists(_filePath))
            {
                using var stream = File.OpenRead(_filePath);
                var serializer = CreateSerializer();
                var result = serializer.Deserialize(stream);
                if (result is List<T> items)
                    _items = items;
                else
                    _items = new List<T>();
            }
            else
            {
                _items = new List<T>();
            }
            State = DatabaseState.Loaded;
            OnLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _items = new List<T>();
            State = DatabaseState.Idle;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var serializer = CreateSerializer();
            using var stream = File.Create(_filePath);
            serializer.Serialize(stream, _items);
            OnSaved?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool Lock()
    {
        if (File.Exists(_lockFilePath))
            return false;
        try
        {
            File.WriteAllText(_lockFilePath, Environment.MachineName + "\n" + DateTime.Now.ToString("o"));
            State = DatabaseState.Locked;
            OnLocked?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Unlock()
    {
        if (File.Exists(_lockFilePath))
        {
            try
            {
                File.Delete(_lockFilePath);
                State = DatabaseState.Loaded;
                OnUnlocked?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }
    }

    public IReadOnlyList<T> GetAll() => _items.AsReadOnly();

    public T? GetById(Guid id) => _items.FirstOrDefault(i => GetId(i) == id);

    public void Add(T item)
    {
        _items.Add(item);
    }

    public bool Update(T item)
    {
        var idx = _items.FindIndex(i => GetId(i) == GetId(item));
        if (idx < 0) return false;
        _items[idx] = item;
        return true;
    }

    public bool Delete(Guid id)
    {
        var item = _items.FirstOrDefault(i => GetId(i) == id);
        if (item == null) return false;
        _items.Remove(item);
        return true;
    }

    public bool IsLocked() => File.Exists(_lockFilePath);

    public string GetLockOwner()
    {
        if (!File.Exists(_lockFilePath)) return string.Empty;
        try
        {
            var lines = File.ReadAllLines(_lockFilePath);
            return lines.Length > 0 ? lines[0] : string.Empty;
        }
        catch { return string.Empty; }
    }
}
