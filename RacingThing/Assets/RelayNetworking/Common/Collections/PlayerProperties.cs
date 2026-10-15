using System;
using System.Collections;
using System.Collections.Generic;

namespace RelayNetworking.Common.Collections
{
    public sealed class PlayerProperties : IEnumerable<KeyValuePair<string, object?>>
    {
        private const int MaxCustomProperties = 256;
        private readonly Dictionary<ReservedPlayerProperty, object?> _reserved = new();
        private readonly Dictionary<string, object?> _custom = new();
        internal IDictionary<ReservedPlayerProperty, object?> Reserved => _reserved;
        internal IDictionary<string, object?> Custom => _custom;

        public object? this[string key]
        {
            get
            {
                if (Enum.TryParse(key, out ReservedPlayerProperty reserved))
                {
                    _reserved.TryGetValue(reserved, out object? value);
                    return value;
                }

                _custom.TryGetValue(key, out object? custom);
                return custom;
            }
            set
            {
                if (Enum.TryParse(key, out ReservedPlayerProperty reserved))
                {
                    _reserved[reserved] = value;
                    return;
                }

                if (!_custom.ContainsKey(key) && _custom.Count >= MaxCustomProperties)
                    return;

                _custom[key] = value;
            }
        }

        public string Username
        {
            get
            {
                if (_reserved.TryGetValue(ReservedPlayerProperty.Username, out object? value))
                    return value as string ?? string.Empty;

                return string.Empty;
            }
            internal set => _reserved[ReservedPlayerProperty.Username] = value;
        }

        public int Count => _reserved.Count + _custom.Count;

        public T? Get<T>(string key)
        {
            object? value = this[key];

            if (value is T result)
                return result;

            return default;
        }

        public bool Contains(string key)
        {
            if (Enum.TryParse(key, out ReservedPlayerProperty reserved))
                return _reserved.ContainsKey(reserved);

            return _custom.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return _custom.Remove(key);
        }

        public void Clear()
        {
            _custom.Clear();
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            foreach (var pair in _reserved)
            {
                yield return new KeyValuePair<string, object?>(pair.Key.ToString(), pair.Value);
            }

            foreach (var pair in _custom)
                yield return pair;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TrySetCustom(string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (!_custom.ContainsKey(key) && _custom.Count >= MaxCustomProperties)
                return false;

            if (value == null)
                _custom.Remove(key);
            else
                _custom[key] = value;

            return true;
        }

        public int GetEstimatedSize()
        {
            int size = 0;

            foreach (var property in _custom)
            {
                size += property.Key.Length * sizeof(char);

                switch (property.Value)
                {
                    case string value:
                        size += value.Length * sizeof(char);
                        break;

                    case bool:
                        size += sizeof(bool);
                        break;

                    case byte:
                        size += sizeof(byte);
                        break;

                    case short:
                        size += sizeof(short);
                        break;

                    case int:
                        size += sizeof(int);
                        break;

                    case long:
                        size += sizeof(long);
                        break;

                    case float:
                        size += sizeof(float);
                        break;

                    case double:
                        size += sizeof(double);
                        break;
                }
            }

            return size;
        }
    }
}