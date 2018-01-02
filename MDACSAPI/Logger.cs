using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MDACSAPI
{
    public static class Logger
    {
        public static event Action<JObject> handler_event;

        public static void WriteCriticalString(string msg)
        {
            var frame = new StackFrame(1);
            var item = new JObject();
            var method = frame.GetMethod();
            var ns_path = new String[] { $"{method.DeclaringType.Namespace}.{method.Name}" };

            item["type"] = "string";
            item["stack"] = JToken.FromObject(ns_path);
            item["value"] = msg;
            item["class"] = "critical";

            handler_event?.Invoke(item);
        }

        public static void WriteDebugString(string msg)
        {
            var frame = new StackFrame(1);
            var item = new JObject();
            var method = frame.GetMethod();
            var ns_path = new String[] { $"{method.DeclaringType.Namespace}.{method.Name}" };

            item["type"] = "string";
            item["stack"] = JToken.FromObject(ns_path);
            item["value"] = msg;
            item["class"] = "debug";

            handler_event?.Invoke(item);
        }
    }
}
