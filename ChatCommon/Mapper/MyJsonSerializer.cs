using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatCommon.Mapper
{
    public static class MyJsonSerializer<User>
    {
        private const string Filename = "registeredUsers.json";

        public static async Task StoreUsersAsync(IEnumerable<User> obj)
        {
            using var stream = new FileStream(Filename, FileMode.OpenOrCreate, FileAccess.Write);
            await JsonSerializer.SerializeAsync(stream, obj);
        }


        public static async Task<IEnumerable<User>> LoadUsersAsync()
        {
            /*
              var bytes = await File.ReadAllBytesAsync(Filename);
                 var json = Encoding.UTF8.GetString(bytes);
                 return JsonSerializer.DeserializeAsync<IEnumerable<T>>(json);
             */

            if (File.Exists(Filename))
            {
                using FileStream fileStream = new FileStream(Filename, FileMode.Open, FileAccess.Read);
                return await JsonSerializer.DeserializeAsync<IEnumerable<User>>(fileStream);
            }

            // return Enumerable.Empty<T>();
            return new List<User>();
        }
    }
}
