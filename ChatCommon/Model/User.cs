using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatCommon.Model
{
    public class User
    {
        public string? UserName { get; set; }
        public string? UserPassword { get; set; }
        public User(string? userName, string? userPassword)
        {
            this.UserName = userName;
            this.UserPassword = userPassword;
        }
    }
}
