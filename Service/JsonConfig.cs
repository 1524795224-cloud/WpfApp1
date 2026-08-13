using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace WpfApp1.Service
{
    public class JsonConfig<T> where T : class, new()
    {
        Person person = new Person { Name = "张三", Age = 30 };
        public void Serial()
        {
            string json = JsonSerializer.Serialize(person);
        }
       

    }
    public class Person
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }
}