using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JpegLibrary.Exp
{
    internal class JpegEncodingTableManager<TTable, TTableBuilder> where TTable : class, IJpegEncodingTable where TTableBuilder : class, IJpegEncodingTableBuilder<TTable>, new()
    {
        private MutableStructList<Entry>? _predefinedTables;
        private MutableStructList<Entry>? _predefinedTableBuilders;
        private MutableStructList<Entry>? _transientTables;

        public void SetPredefinedTable(bool isDc, byte identifier, TTable table)
        {
            MutableStructList<Entry> predefinedTables = EnsureInitialized(ref _predefinedTables);
            AddOrUpdate(predefinedTables, Entry.Create(isDc, identifier, table));
            RemoveEntry(_predefinedTableBuilders, isDc, identifier);
        }

        public TTable? GetPredefinedTable(bool isDc, byte identifier)
        {
            ref Entry entry = ref TryFind(_predefinedTables, isDc, identifier);
            if (!Unsafe.IsNullRef(ref entry))
            {
                return entry.GetTable();
            }
            entry = ref TryFind(_predefinedTableBuilders, isDc, identifier);
            if (!Unsafe.IsNullRef(ref entry))
            {
                return entry.GetTable();
            }
            return null;
        }

        public void SetPredefinedTableBuilder(bool isDc, byte identifier)
        {
            MutableStructList<Entry> predefinedTableBuilders = EnsureInitialized(ref _predefinedTableBuilders);
            AddOrUpdate(predefinedTableBuilders, Entry.CreateBuilder(isDc, identifier));
            RemoveEntry(_predefinedTables, isDc, identifier);
        }

        public TTableBuilder? GetPredefinedTableBuilder(bool isDc, byte identifier)
        {
            ref Entry entry = ref TryFind(_predefinedTableBuilders, isDc, identifier);
            if (Unsafe.IsNullRef(ref entry))
            {
                return null;
            }
            if (entry.State != EntryState.Builder)
            {
                return null;
            }
            return entry.InitializeBuilder();
        }

        public bool BuildPredefinedTables(bool optimal)
        {
            MutableStructList<Entry>? predefinedTableBuilders = _predefinedTableBuilders;
            if (predefinedTableBuilders is null)
            {
                return false;
            }

            bool isAnyBuilt = false;
            int count = predefinedTableBuilders.Count;
            for (int i = 0; i < count; i++)
            {
                if (predefinedTableBuilders[i].State == EntryState.Builder)
                {
                    predefinedTableBuilders[i].Build(optimal);
                    isAnyBuilt = true;
                }
            }

            return isAnyBuilt;
        }

        public void SetTransientTable(bool isDc, byte identifier, TTable table)
        {
            MutableStructList<Entry> transientTables = EnsureInitialized(ref _transientTables);
            AddOrUpdate(transientTables, Entry.Create(isDc, identifier, table));
        }

        public void SetTransientTableBuilder(bool isDc, byte identifier)
        {
            MutableStructList<Entry> transientTables = EnsureInitialized(ref _transientTables);
            AddOrUpdate(transientTables, Entry.CreateBuilder(isDc, identifier));
        }

        public TTableBuilder? GetTransientTableBuilder(bool isDc, byte identifier)
        {
            ref Entry entry = ref TryFind(_transientTables, isDc, identifier);
            if (Unsafe.IsNullRef(ref entry))
            {
                return null;
            }
            if (entry.State != EntryState.Builder)
            {
                return null;
            }
            return entry.InitializeBuilder();
        }

        public TTable? GetCurrentTable(bool isDc, byte identifier)
        {
            ref Entry entry = ref TryFind(_transientTables, isDc, identifier);
            if (!Unsafe.IsNullRef(ref entry))
            {
                if (entry.State == EntryState.Builder)
                {
                    ThrowInvalidOperationException();
                }
                return entry.GetTable();
            }
            entry = ref TryFind(_predefinedTableBuilders, isDc, identifier);
            if (!Unsafe.IsNullRef(ref entry))
            {
                if (entry.State == EntryState.Builder)
                {
                    ThrowInvalidOperationException();
                }
                return entry.GetTable();
            }
            entry = ref TryFind(_predefinedTables, isDc, identifier);
            if (!Unsafe.IsNullRef(ref entry))
            {
                if (entry.State == EntryState.Builder)
                {
                    ThrowInvalidOperationException();
                }
                return entry.GetTable();
            }
            return null;
        }

        public bool BuildTransientTables(bool optimal)
        {
            MutableStructList<Entry>? transientTables = _transientTables;
            if (transientTables is null)
            {
                return false;
            }

            bool isAnyBuilt = false;
            int count = transientTables.Count;
            for (int i = 0; i < count; i++)
            {
                ref Entry entry = ref transientTables[i];
                if (entry.State == EntryState.Builder)
                {
                    entry.Build(optimal);
                    isAnyBuilt = true;
                }
            }

            return isAnyBuilt;
        }

        public int GetTotalBytesRequired()
        {
            int totalBytes = 0;

            MutableStructList<Entry>? tables = _predefinedTables;
            if (!(tables is null))
            {
                int count = tables.Count;
                for (int i = 0; i < count; i++)
                {
                    ref Entry entry = ref tables[i];
                    if (entry.State == EntryState.Builder)
                    {
                        ThrowInvalidOperationException();
                    }
                    if (entry.State == EntryState.Table)
                    {
                        totalBytes += entry.BytesRequired;
                        totalBytes++;
                    }
                }
            }

            tables = _predefinedTableBuilders;
            if (!(tables is null))
            {
                int count = tables.Count;
                for (int i = 0; i < count; i++)
                {
                    ref Entry entry = ref tables[i];
                    if (entry.State == EntryState.Builder)
                    {
                        ThrowInvalidOperationException();
                    }
                    if (entry.State == EntryState.Table)
                    {
                        totalBytes += entry.BytesRequired;
                        totalBytes++;
                    }
                }
            }

            tables = _transientTables;
            if (!(tables is null))
            {
                int count = tables.Count;
                for (int i = 0; i < count; i++)
                {
                    ref Entry entry = ref tables[i];
                    if (entry.State == EntryState.Builder)
                    {
                        ThrowInvalidOperationException();
                    }
                    if (entry.State == EntryState.Table)
                    {
                        totalBytes += entry.BytesRequired;
                        totalBytes++;
                    }
                }
            }

            return totalBytes;
        }

        public void WriteTables(ref JpegWriter writer)
        {
            MutableStructList<Entry>? tables = _predefinedTables;
            if (!(tables is null))
            {
                int count = tables.Count;
                for (int i = 0; i < count; i++)
                {
                    ref Entry entry = ref tables[i];
                    if (entry.State == EntryState.Builder)
                    {
                        ThrowInvalidOperationException();
                    }
                    if (entry.State == EntryState.Table)
                    {
                        int bytesRequired = 1 + entry.BytesRequired;
                        Span<byte> buffer = writer.GetSpan(bytesRequired);
                        buffer[0] = (byte)(entry.TableClass << 4 | (entry.Identifier & 0xf));
                        entry.Write(buffer.Slice(1), out _);
                        writer.Advance(bytesRequired);
                    }
                }
            }

            tables = _predefinedTableBuilders;
            if (!(tables is null))
            {
                int count = tables.Count;
                for (int i = 0; i < count; i++)
                {
                    ref Entry entry = ref tables[i];
                    if (entry.State == EntryState.Builder)
                    {
                        ThrowInvalidOperationException();
                    }
                    if (entry.State == EntryState.Table)
                    {
                        int bytesRequired = 1 + entry.BytesRequired;
                        Span<byte> buffer = writer.GetSpan(bytesRequired);
                        buffer[0] = (byte)(entry.TableClass << 4 | (entry.Identifier & 0xf));
                        entry.Write(buffer.Slice(1), out _);
                        writer.Advance(bytesRequired);
                    }
                }
            }

            tables = _transientTables;
            if (!(tables is null))
            {
                int count = tables.Count;
                for (int i = 0; i < count; i++)
                {
                    ref Entry entry = ref tables[i];
                    if (entry.State == EntryState.Builder)
                    {
                        ThrowInvalidOperationException();
                    }
                    if (entry.State == EntryState.Table)
                    {
                        int bytesRequired = 1 + entry.BytesRequired;
                        Span<byte> buffer = writer.GetSpan(bytesRequired);
                        buffer[0] = (byte)(entry.TableClass << 4 | (entry.Identifier & 0xf));
                        entry.Write(buffer.Slice(1), out _);
                        writer.Advance(bytesRequired);
                    }
                }
            }
        }

        private static MutableStructList<Entry> EnsureInitialized(ref MutableStructList<Entry>? list)
        {
            if (list is null)
            {
                return list = new MutableStructList<Entry>();
            }
            return list;
        }

        private static void RemoveEntry(MutableStructList<Entry>? list, bool isDc, byte identifier)
        {
            if (list is null)
            {
                return;
            }
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (list[i].IsMatch(isDc, identifier))
                {
                    list.RemoveAt(i);
                    return;
                }
            }
        }

        private static void AddOrUpdate(MutableStructList<Entry> list, Entry entry)
        {
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                if (list[i].IsMatch(entry))
                {
                    list[i] = entry;
                    return;
                }
            }
            list.Add(entry);
        }

        private static ref Entry TryFind(MutableStructList<Entry>? list, bool isDc, byte identifier)
        {
            if (list is null)
            {
                return ref Unsafe.NullRef<Entry>();
            }
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                ref Entry entry = ref list[i];
                if (entry.IsMatch(isDc, identifier))
                {
                    return ref entry;
                }
            }
            return ref Unsafe.NullRef<Entry>();
        }

        struct Entry
        {
            // This can be ether the encoding table irself or the table builder.
            private object? _table;
            private bool _isDcTable;
            private byte _identifier;
            private byte _state; // 0-table builder, 1-table, 2-written

            public EntryState State => (EntryState)_state;

            public byte TableClass => _isDcTable ? (byte)0 : (byte)1;
            public byte Identifier => _identifier;

            public static Entry Create(bool isDcTable, byte identifier, TTable table)
            {
                return new Entry
                {
                    _table = table,
                    _isDcTable = isDcTable,
                    _identifier = identifier,
                    _state = 1
                };
            }

            public static Entry CreateBuilder(bool isDcTable, byte identifier)
            {
                return new Entry
                {
                    _isDcTable = isDcTable,
                    _identifier = identifier,
                    _state = 0
                };
            }

            public bool IsMatch(bool isDcTable, byte identifier)
            {
                return _isDcTable == isDcTable && _identifier == identifier;
            }

            public bool IsMatch(Entry other)
            {
                return _isDcTable == other._isDcTable && _identifier == other._identifier;
            }

            public ushort BytesRequired => _state == 1 ? ((TTable?)_table!).BytesRequired : ThrowInvalidOperationException<ushort>();

            public TTableBuilder InitializeBuilder()
            {
                if (_state != 0)
                {
                    ThrowInvalidOperationException();
                }

                if (_table is null)
                {
                    var builder = new TTableBuilder();
                    _table = builder;
                    return builder;
                }

                return (TTableBuilder)_table;
            }

            public void Build(bool optimal)
            {
                if (_state != 0)
                {
                    ThrowInvalidOperationException();
                }

                var tableBuilder = (TTableBuilder?)_table;
                tableBuilder ??= new TTableBuilder();

                _table = tableBuilder.Build(optimal);
                _state = 1;
            }

            public TTable GetTable()
            {
                if (_state == 0)
                {
                    ThrowInvalidOperationException();
                }

                var table = (TTable?)_table;
                if (table is null)
                {
                    ThrowInvalidOperationException();
                }

                return table;
            }

            public void Write(Span<byte> buffer, out int bytesWritten)
            {
                if (_state != 1)
                {
                    ThrowInvalidOperationException();
                }

                if (!((TTable?)_table!).TryWrite(buffer, out bytesWritten))
                {
                    ThrowInvalidOperationException();
                }

                _state = 2;
            }
        }

        private enum EntryState
        {
            Builder = 0,
            Table = 1,
            WrittenTable = 2,
        }

        private static T ThrowInvalidOperationException<T>()
        {
            throw new InvalidOperationException();
        }

        [DoesNotReturn]
        private static void ThrowInvalidOperationException()
        {
            throw new InvalidOperationException();
        }
    }
}
