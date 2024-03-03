using Il2CppAssets.Scripts.PeroTools.Nice.Interface;
using MelonLoader;
using System.Security;
using System.Text;

namespace BetterNativeHook.Extensions
{

    [SecurityCritical]
    [PatchShield]
    public static class Extensions
    {
        public static Il2CppSystem.UInt16 BoxUInt16(this ushort value)
        {
            return new Il2CppSystem.UInt16
            {
                m_value = value
            };
        }
        public static Il2CppSystem.UInt32 BoxUInt32(this uint value)
        {
            return new Il2CppSystem.UInt32
            {
                m_value = value
            };
        }
        public unsafe static Il2CppSystem.UInt32 BoxUInt32(this float value)
        {
            return new Il2CppSystem.UInt32
            {
                m_value = *(uint*)&value
            };
        }
        public static Il2CppSystem.UInt64 BoxUInt64(this ulong value)
        {
            return new Il2CppSystem.UInt64
            {
                m_value = value
            };
        }
        public unsafe static Il2CppSystem.UInt64 BoxUInt64(this double value)
        {
            return new Il2CppSystem.UInt64
            {
                m_value = *(ulong*)&value
            };
        }

        public static Il2CppSystem.Int16 BoxInt16(this short value)
        {
            return new Il2CppSystem.Int16
            {
                m_value = value
            };
        }
        public static Il2CppSystem.Int32 BoxInt32(this int value)
        {
            return new Il2CppSystem.Int32
            {
                m_value = value
            };
        }
        public unsafe static Il2CppSystem.Int32 BoxInt32(this float value)
        {
            return new Il2CppSystem.Int32
            {
                m_value = *(int*)&value
            };
        }
        public static Il2CppSystem.Int64 BoxInt64(this long value)
        {
            return new Il2CppSystem.Int64
            {
                m_value = value
            };
        }
        public unsafe static Il2CppSystem.Int64 BoxInt64(this double value)
        {
            return new Il2CppSystem.Int64
            {
                m_value = *(long*)&value
            };
        }
        public unsafe static Il2CppSystem.Decimal BoxDecimal(this decimal value)
        {
            return new Il2CppSystem.Decimal((double)value);
        }
        public unsafe static Il2CppSystem.Decimal BoxDecimal(this double value)
        {
            return new Il2CppSystem.Decimal(value);
        }
        public unsafe static Il2CppSystem.Decimal BoxDecimal(this long value)
        {
            return new Il2CppSystem.Decimal(value);
        }
        public unsafe static Il2CppSystem.Decimal BoxDecimal(this ulong value)
        {
            return new Il2CppSystem.Decimal(value);
        }



        public static Il2CppSystem.Object BoxUInt16Object(this ushort value)
        {
            return value.BoxUInt16().BoxIl2CppObject();
        }
        public static Il2CppSystem.Object BoxUInt32Object(this uint value)
        {
            return value.BoxUInt32().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxUInt32Object(this float value)
        {
            return value.BoxUInt32().BoxIl2CppObject();
        }
        public static Il2CppSystem.Object BoxUInt64Object(this ulong value)
        {
            return value.BoxUInt64().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxUInt64Object(this double value)
        {
            return value.BoxUInt64().BoxIl2CppObject();
        }

        public static Il2CppSystem.Object BoxInt16Object(this short value)
        {
            return value.BoxInt16().BoxIl2CppObject();
        }
        public static Il2CppSystem.Object BoxInt32Object(this int value)
        {
            return value.BoxInt32().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxInt32Object(this float value)
        {
            return value.BoxInt32().BoxIl2CppObject();
        }
        public static Il2CppSystem.Object BoxInt64Object(this long value)
        {
            return value.BoxInt64().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxInt64Object(this double value)
        {
            return value.BoxInt64().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxDecimalObject(this decimal value)
        {
            return value.BoxDecimal().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxDecimalObject(this double value)
        {
            return value.BoxDecimal().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxDecimalObject(this long value)
        {
            return value.BoxDecimal().BoxIl2CppObject();
        }
        public unsafe static Il2CppSystem.Object BoxDecimalObject(this ulong value)
        {
            return value.BoxDecimal().BoxIl2CppObject();
        }



        public static T GetResult<T>(this IVariable variable)
        {
            return VariableUtils.GetResult<T>(variable);
        }
        public static void SetResult<T>(this IVariable variable, Il2CppSystem.Object value)
        {
            VariableUtils.SetResult(variable, value);
        }
        public static T? GetResultOrDefault<T>(this IVariable variable)
        {
            try
            {
                return VariableUtils.GetResult<T>(variable);
            }
            catch (Exception)
            {
                return default;
            }
        }
        public static List<Il2CppSystem.Object> ToManaged(this Il2CppSystem.Collections.IEnumerable cpList)
        {
            if (cpList is null)
                return null!;
            var list = new List<Il2CppSystem.Object>();
            foreach (var item in cpList)
            {
                list.Add(item);
            }
            return list;
        }
        public static List<T> ToManaged<T>(this Il2CppSystem.Collections.IEnumerable cpList)
        {
            if (cpList is null)
                return null!;
            var list = new List<T>();
            foreach (var item in cpList)
            {
                list.Add((T)(object)item);
            }
            return list;
        }
        public static List<T> ToManaged<T>(this Il2CppSystem.Collections.IEnumerable cpList, Func<Il2CppSystem.Object, T> transformer)
        {
            if (transformer is null)
            {
                throw new ArgumentNullException(nameof(transformer));
            }
            if (cpList is null)
                return null!;
            var list = new List<T>();
            foreach (var item in cpList)
            {
                list.Add(transformer.Invoke(item));
            }
            return list;
        }
        public static List<T> ToManaged<T>(this Il2CppSystem.Collections.Generic.List<T> cpList)
        {
            if (cpList is null)
                return null!;
            var list = new List<T>();
            foreach (var item in cpList)
            {
                list.Add(item);
            }
            return list;
        }
        public static Dictionary<TKey, TValue> ToManaged<TKey, TValue>(this Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue> cpDictionary) where TKey: notnull
        {
            if (cpDictionary is null)
                return null!;
            var dictionary = new Dictionary<TKey, TValue>();
            foreach (var entry in cpDictionary)
            {
                dictionary[entry.Key] = entry.Value;
            }
            return dictionary;
        }

        public static Il2CppSystem.Collections.Generic.List<T> ToIL2CPP<T>(this IEnumerable<T> collection)
        {
            if (collection is null)
                return null!;
            var result = new Il2CppSystem.Collections.Generic.List<T>();
            foreach (var item in collection)
            {
                result.Add(item);
            }
            return result;
        }
        public static Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue> ToIL2CPP<TKey, TValue>(this Dictionary<TKey, TValue> dictionary) where TKey: notnull
        {
            if (dictionary is null)
                return null!;
            var result = new Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue>();
            foreach (var entry in dictionary)
            {
                result[entry.Key] = entry.Value;
            }
            return result;
        }
        public static byte[] ReadFully(this Stream stream, int initialLength = 0)
        {
            if (initialLength < 1)
            {
                initialLength = 32768;
            }

            byte[] buffer = new byte[initialLength];
            int read = 0;

            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += chunk;

                if (read == buffer.Length)
                {
                    int nextByte = stream.ReadByte();
                    if (nextByte == -1)
                    {
                        return buffer;
                    }
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    newBuffer[read] = (byte)nextByte;
                    buffer = newBuffer;
                    read++;
                }
            }
            byte[] ret = new byte[read];
            Array.Copy(buffer, ret, read);
            return ret;
        }
        public static Stream ToStream(this string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream, Encoding.ASCII);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
        public static string ReadString(this Stream stream)
        {
            return new StreamReader(stream).ReadToEnd();
        }
    }
}
