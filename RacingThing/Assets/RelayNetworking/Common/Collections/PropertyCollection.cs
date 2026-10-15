using System.Collections;
using System.Collections.Generic;

namespace RelayNetworking.Common.Collections
{
    public class PropertyCollection : IEnumerable<KeyValuePair<string, object?>>
    {
        private const int MaxStoredProperties = 256;
        protected readonly Dictionary<string, object?> _properties = new();
        public object? this[string key]
        {
            get
            {
                _properties.TryGetValue(key, out object? value);
                return value;
            }
            set
            {
                if (!_properties.ContainsKey(key) && _properties.Count >= MaxStoredProperties)
                    return;
                _properties[key] = value;
            }
        }
        public int Count => _properties.Count;
        public IReadOnlyDictionary<string, object?> Values => _properties;
        public bool Contains(string key)
        {
            return _properties.ContainsKey(key);
        }
        public bool Remove(string key)
        {
            return _properties.Remove(key);
        }
        public void Clear()
        {
            _properties.Clear();
        }
        public T? Get<T>(string key)
        {
            if (_properties.TryGetValue(key, out object? value) && value is T result)
                return result;
            return default;
        }
        public bool TryGet<T>(string key, out T value)
        {
            if (_properties.TryGetValue(key, out object? obj) && obj is T result)
            {
                value = result;
                return true;
            }
            value = default!;
            return false;
        }
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            return _properties.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}