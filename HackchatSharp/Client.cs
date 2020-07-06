using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Net;
using System.Security.Authentication;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

/*
 * Required packages
 * 
 * Install-Package WebSocketSharp -Pre
 * Install-Package Newtonsoft.Json -Version 12.0.3
 */

namespace HackchatSharp
{
    public class Client
    {
        private WebSocket ws = null;
        private List<User> onlineUsers = new List<User>();
        private string url = "wss://hack.chat/chat-ws";
        private string nick = null;
        private string channel = null;

        private string prefix = "/";
        public string Prefix { get { return this.prefix; } set { this.prefix = value; } }
        private Dictionary<string, Action<Message, string[], string[]>> commands = new Dictionary<string, Action<Message, string[], string[]>>();
        public Dictionary<string, Action<Message, string[], string[]>> Commands { get { return this.commands; } set { this.commands = value; } }

        public Action OnConnect = null;
        public Action OnDisconnect = null;
        public Action<Message> OnMessage = null;
        public Action<User[]> OnOnlineSet = null;
        public Action<User> OnOnlineAdd = null;
        public Action<User> OnOnlineRemove = null;
        public Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> ServerCertificateValidationCallback = null;

        public Client(string url = null)
        {
            this.ws = new WebSocket(url == null ? this.url : url);

            this.ws.OnOpen += this.ws_OnOpen;
            this.ws.OnClose += this.ws_OnClose;
            this.ws.OnMessage += this.ws_OnMessage;
            this.ws.OnError += this.ws_OnError;

            ClientSslConfiguration sslconf = new ClientSslConfiguration("hack.chat");
            sslconf.EnabledSslProtocols = SslProtocols.Tls12;
            sslconf.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                if (this.ServerCertificateValidationCallback != null)
                {
                    return this.ServerCertificateValidationCallback(sender, certificate, chain, sslPolicyErrors);
                }

                return true;
            };
            this.ws.SslConfiguration = sslconf;

            this.ws.ConnectAsync();
        }

        public void Disconnect()
        {
            this.ws.CloseAsync();
        }

        public void Send(string json)
        {
            this.ws.SendAsync(Encoding.UTF8.GetBytes(json), null);
        }

        public void Say(string text)
        {
            JObject obj = new JObject();
            obj.Add("cmd", "chat");
            obj.Add("text", text);

            this.Send(obj.ToString(Formatting.None));
        }

        public void Join(string nick, string channel)
        {
            this.nick = nick;
            this.channel = channel;

            JObject obj = new JObject();
            obj.Add("cmd", "join");
            obj.Add("channel", channel);
            obj.Add("nick", nick);

            this.Send(obj.ToString(Formatting.None));
        }

        public void Join(string nick, string password, string channel)
        {
            this.Join(String.Join("#", nick, password), channel);
        }

        private void ws_OnOpen(object sender, EventArgs e)
        {
            this.OnConnect?.Invoke();
        }

        private void ws_OnClose(object sender, CloseEventArgs e)
        {
            this.OnDisconnect?.Invoke();
        }

        private void ws_OnMessage(object sender, MessageEventArgs e)
        {
            string json = Encoding.UTF8.GetString(e.RawData);
            JObject data = JObject.Parse(json);
            string cmd = (string)data["cmd"];

            if (cmd == "chat")
            {
                Message msg = new Message
                {
                    Time = (long)data["time"],
                    Nick = (string)data["nick"],
                    Trip = data.ContainsKey("trip") && (string)data["trip"] != null ? (string)data["trip"] : null,
                    Text = (string)data["text"],
                    Type = MessageTypes.Chat,
                };

                if (data.ContainsKey("admin"))
                {
                    msg.Role = UserRoles.Admin;
                }
                else if (data.ContainsKey("mod"))
                {
                    msg.Role = UserRoles.Mod;
                }

                if (msg.Text.StartsWith(this.prefix) && msg.Text.Length > this.prefix.Length)
                {
                    string[] parts = msg.Text.Split(' ');
                    string _cmd = parts[0].Substring(this.prefix.Length);
                    string[] arr = parts.Skip(1).ToArray();
                    string[] pars = this.Parse(msg.Text).Skip(1).ToArray();

                    if (this.commands.ContainsKey(_cmd))
                    {
                        this.commands[_cmd].Invoke(msg, pars, arr);
                    }
                } else
                {
                    this.OnMessage?.Invoke(msg);
                }
            }
            else if (cmd == "info")
            {
                Message msg = new Message
                {
                    Time = (long)data["time"],
                    Text = (string)data["text"],
                    Type = MessageTypes.Info,
                    Role = UserRoles.Server,
                };

                this.OnMessage?.Invoke(msg);
            }
            else if (cmd == "warn")
            {
                Message msg = new Message
                {
                    Time = (long)data["time"],
                    Text = (string)data["text"],
                    Type = MessageTypes.Warn,
                    Role = UserRoles.Server,
                };

                this.OnMessage?.Invoke(msg);
            }
            else if (cmd == "onlineSet")
            {
                JArray nicks = (JArray)data["nicks"];
                foreach (JToken nick in nicks)
                {
                    this.onlineUsers.Add(new User { 
                        Nick = nick.ToString(),
                    });
                }

                this.OnOnlineSet?.Invoke(this.onlineUsers.ToArray());
            }
            else if (cmd == "onlineAdd")
            {
                User user = new User
                {
                    Nick = (string)data["nick"],
                };
                this.onlineUsers.Add(user);

                this.OnOnlineAdd?.Invoke(user);
            }
            else if (cmd == "onlineRemove")
            {
                string nick = (string)data["nick"];

                for (int i = 0; i < this.onlineUsers.Count; i++)
                {
                    User user = this.onlineUsers[i];
                    if (user.Nick == nick)
                    {
                        this.onlineUsers.RemoveAt(i);
                        this.OnOnlineRemove?.Invoke(user);
                        break;
                    }
                }
            }
        }

        private void ws_OnError(object sender, ErrorEventArgs e)
        {

        }

        private string[] Parse(string text)
        {
            var words = text.Replace("\t", " ").Split(" "[0]); var result = new List<string>(); bool flag = false;
            var str = "";
            for (var i = 0; i < words.Length; i++)
            {
                if (flag == true)
                {
                    str += words[i] + " ";
                }
                if (flag == false && !words[i].StartsWith("\""))
                {
                    result.Add(words[i]);
                }
                if (words[i].StartsWith("\"") && flag == false)
                {
                    str += words[i].Remove(0, 1) + " ";
                    flag = true;
                }
                if (words[i].EndsWith("\"") && flag == true)
                {
                    result.Add(str.Remove(str.Length - 2, 2)); str = "";
                    flag = false;
                }
            }
            return result.ToArray();
        }
    }

    public enum UserRoles
    {
        Server,
        Admin,
        Mod,
        User,
    }

    public enum MessageTypes
    {
        Chat,
        Info,
        Warn,
    }

    public class User
    {
        public string Nick = "";

        public User()
        {

        }
    }

    public class Message
    {
        public string Nick = "";
        public string Trip = "";
        public string Text = "";
        public UserRoles Role = UserRoles.User;
        public MessageTypes Type = MessageTypes.Chat;
        public long Time = 0;

        public Message()
        {

        }
    }
}
