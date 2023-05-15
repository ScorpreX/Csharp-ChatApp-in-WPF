using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatCommon.Mapper
{
    public class ByteSerializer
    {
        public static async Task<IEnumerable<byte>> SaveDTOAsync<T>(T obj)
        {
            //using var stream = new MemoryStream();
            //await JsonSerializer.SerializeAsync(stream, obj);

            //return stream.ToArray();
            //return new List<byte>(stream.ToArray());
            return JsonSerializer.SerializeToUtf8Bytes(obj).ToList();
        }

        public static async Task<T> GetDTOAssync<T>(IEnumerable<byte> bytelist)
        {
            //using var stream = new MemoryStream(bytelist.ToArray());
            //return await JsonSerializer.DeserializeAsync<T>(stream);

            string asd = $"{Encoding.UTF8.GetString(bytelist.ToArray())}";

            return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytelist.ToArray()));
        }
    }
}
