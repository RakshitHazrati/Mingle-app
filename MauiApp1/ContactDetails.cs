using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiApp1
{
    public  class ContactDetails
    {
        public string Name { get; private set; }
        public string phone { get; private set; }

        public ContactDetails(string name,string phone)
        {
            Name = name;
            this.phone = phone;
        }
    }
}
