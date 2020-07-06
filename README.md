# HackchatSharp
HackchatSharp is a C# library for hack.chat.

#### Required packages
- WebSocketSharp
- Newtonsoft.Json

#### Example
```csharp
Client client = new Client();

/* basic callbacks */
client.OnOnlineAdd = user => { Console.WriteLine("User '" + user.Nick + "' joined"); };
client.OnOnlineRemove = user => { Console.WriteLine("User '" + user.Nick + "' left"); };
client.OnOnlineSet = users => { Console.WriteLine(users.Length.ToString() + " users online"); };
client.OnDisconnect = () => { Console.WriteLine("Disconnected from hack.chat"); };
        
client.OnConnect = () => {
    Console.WriteLine("Connected to hack.chat");

    // we send join once 
    // we are connected to server
    client.Join("SharpBot", "NoStealingMyPassword", "programming");
};

client.OnMessage = msg => {
    Console.WriteLine(msg.Nick + (msg.Trip != null ? "#" + msg.Trip : "") + " [" + msg.Role.ToString() + "]: " + msg.Text);

    if (msg.Text == "ping")
    {
        client.Say("pong");
    }
};

/* add some nice commands */
client.Prefix = "--";

client.Commands.Add("test", (Message message, string[] pars, string[] arr) =>
{
    client.Say("@" + message.Nick + ", " + String.Join(", ", pars));
});

Console.ReadLine();
```
