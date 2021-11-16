# HackchatSharp
HackchatSharp is a C# library for hack.chat.

#### Required packages
- WebSocketSharp
- Newtonsoft.Json

#### Example
```csharp
using System;
using System.Linq;
using HackchatSharp;

class Program
{
    static void Main(string[] _args)
    {
        var client = new HackClient();

        client.Prefix = "-";

        client.OnConnect += OnConnect;
        client.OnDisconnect += OnDisconnect;
        client.OnMessage += OnMessage;
        client.OnOnlineSet += OnOnlineSet;
        client.OnOnlineAdd += OnOnlineAdd;
        client.OnOnlineRemove += OnOnlineRemove;

        /**
         * Define a new command
         */
        client.Commands["ping"] = args =>
        {
            /**
             * args.Client
             * args.User
             * args.Message
             * args.Args
             * args.Parsed
             */

            args.User.Say("pong!");
        };

        client.Connect();
    }

    static void OnConnect(object sender, HackClient.HCConnectEventArgs e)
    {
        Console.WriteLine("Connected!");

        /**
         * Try to join a channel when connected
         * to WebSocket server
         */
        e.Client.Join("SharpBot", "NoStealingMyPassword", "programming");
    }

    static void OnDisconnect(object sender, HackClient.HCDisconnectEventArgs e)
    {
        Console.WriteLine("Disconnected!");
    }

    static void OnMessage(object sender, HackClient.HCMessageEventArgs e)
    {
        string text = $"{e.User}: {e.Message.Text}";

        if (e.User.Nick == null)
            text = $"{e.Message.Text}";

        Console.WriteLine(text);

        if (e.Message.Type == MessageTypes.Chat)
        {
            // ...
        }
        else if (e.Message.Type == MessageTypes.Info)
        {
            // ...
        }
        else if (e.Message.Type == MessageTypes.Warn)
        {
            // ...
        }
    }

    static void OnOnlineSet(object sender, HackClient.HCOnlineSetEventArgs e)
    {
        Console.WriteLine($"Users online: {String.Join(", ", e.Users.Select(user => user.ToString()))}");
    }

    static void OnOnlineAdd(object sender, HackClient.HCOnlineAddEventArgs e)
    {
        Console.WriteLine($"{e.User} joined");
    }

    static void OnOnlineRemove(object sender, HackClient.HCOnlineRemoveEventArgs e)
    {
        Console.WriteLine($"{e.User} left");
    }
}
```
