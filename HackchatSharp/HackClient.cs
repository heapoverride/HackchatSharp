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
    public class HackClient
    {
        private WebSocket ws = null;
        private string nick = null;
        public string Nick { get { return this.nick; } }
        private string password = null;
        public string Password { get { return this.password; } }
        private string channel = null;
        public string Channel { get { return this.channel; } }

        private List<User> onlineUsers = new List<User>();
        public User[] OnlineUsers { get { return this.onlineUsers.ToArray(); } }
        public string Prefix = "/";

        public Dictionary<string, Action<CommandArgs>> Commands
            = new Dictionary<string, Action<CommandArgs>>();

        public Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> ServerCertificateValidationCallback = null;

        public class HCConnectEventArgs : EventArgs
        {
            public HackClient Client;
        }
        public EventHandler<HCConnectEventArgs> OnConnect;

        public class HCDisconnectEventArgs : EventArgs
        {
            public HackClient Client;
        }
        public EventHandler<HCDisconnectEventArgs> OnDisconnect;

        public class HCMessageEventArgs : EventArgs
        {
            public HackClient Client;
            public User User;
            public Message Message;
        }
        public EventHandler<HCMessageEventArgs> OnMessage;

        public class HCOnlineSetEventArgs : EventArgs
        {
            public HackClient Client;
            public User[] Users;
        }
        public EventHandler<HCOnlineSetEventArgs> OnOnlineSet;

        public class HCOnlineAddEventArgs : EventArgs
        {
            public HackClient Client;
            public User User;
        }
        public EventHandler<HCOnlineAddEventArgs> OnOnlineAdd;

        public class HCOnlineRemoveEventArgs : EventArgs
        {
            public HackClient Client;
            public User User;
        }
        public EventHandler<HCOnlineRemoveEventArgs> OnOnlineRemove;
        public EventHandler<ErrorEventArgs> OnError;


        public HackClient(string url = "wss://hack.chat/chat-ws")
        {
            Uri uri = new Uri(url);
            ws = new WebSocket(uri.ToString());

            ws.Log.Output = (data, str) => {
                // ...
            };

            ws.OnOpen += this.ws_OnOpen;
            ws.OnClose += this.ws_OnClose;
            ws.OnMessage += this.ws_OnMessage;
            ws.OnError += this.ws_OnError;

            ClientSslConfiguration sslconf = new ClientSslConfiguration(uri.Host);
            sslconf.EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12 | SslProtocols.Tls11;

            sslconf.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
                if (this.ServerCertificateValidationCallback != null)
                {
                    return this.ServerCertificateValidationCallback(sender, certificate, chain, sslPolicyErrors);
                }

                return true;
            };

            ws.SslConfiguration = sslconf;
        }

        public void SetProxy(string url, string username = null, string password = null)
        {
            ws.SetProxy(url, username, password);
        }

        public void Connect()
        {
            ws.Connect();
        }

        public void ConnectAsync()
        {
            // May not be supported on some platforms
            ws.ConnectAsync();
        }

        public void Disconnect()
        {
            ws.Close();
        }

        public void DisconnectAsync()
        {
            ws.CloseAsync();
        }

        public void SendAsync(string json)
        {
            // May not be supported on some platforms
            ws.SendAsync(Encoding.UTF8.GetBytes(json), null);
        }

        public void Send(string json)
        {
            ws.Send(Encoding.UTF8.GetBytes(json));
        }

        public void Say(string text)
        {
            JObject obj = new JObject();
            obj.Add("cmd", "chat");
            obj.Add("text", text);

            Send(obj.ToString(Formatting.None));
        }

        public void Join(string nick, string channel)
        {
            this.nick = nick;
            this.channel = channel;

            JObject obj = new JObject();
            obj.Add("cmd", "join");
            obj.Add("channel", channel);
            obj.Add("nick", nick);

            Send(obj.ToString(Formatting.None));
        }

        public void Join(string nick, string password, string channel)
        {
            this.nick = nick;
            this.password = password;
            this.channel = channel;

            JObject obj = new JObject();
            obj.Add("cmd", "join");
            obj.Add("channel", channel);
            obj.Add("nick", String.Join("#", nick, password));

            Send(obj.ToString(Formatting.None));
        }

        private void ws_OnOpen(object sender, EventArgs e)
        {
            OnConnect?.Invoke(this, new HCConnectEventArgs
            {
                Client = this
            });
        }

        private void ws_OnClose(object sender, CloseEventArgs e)
        {
            OnDisconnect?.Invoke(this, new HCDisconnectEventArgs
            {
                Client = this
            });
        }

        private void ws_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                string json = Encoding.UTF8.GetString(e.RawData);
                JObject data = JObject.Parse(json);
                string cmd = (string)data["cmd"];

                if (cmd == "chat")
                {
                    User user = new User
                    {
                        Client = this,
                        Nick = (string)data["nick"] ?? null,
                        Trip = data.ContainsKey("trip") && (string)data["trip"] != null && ((string)data["trip"]).Length > 0 ? (string)data["trip"] : null,
                    };

                    Message msg = new Message
                    {
                        Client = this,
                        Time = (long)data["time"],
                        Text = (string)data["text"],
                        Type = MessageTypes.Chat
                    };

                    if (data.ContainsKey("admin"))
                    {
                        user.Role = UserRoles.Admin;
                    }
                    else if (data.ContainsKey("mod"))
                    {
                        user.Role = UserRoles.Mod;
                    }

                    if ((user.Nick != null && user.Nick != Nick) && msg.Text.StartsWith(Prefix) && msg.Text.Length > Prefix.Length)
                    {
                        string[] parts = msg.Text.Split(' ');
                        string _cmd = parts[0].Substring(Prefix.Length);
                        string[] arr = parts.Skip(1).ToArray();
                        string[] pars = this.Parse(msg.Text).Skip(1).ToArray();

                        if (Commands.ContainsKey(_cmd))
                        {
                            Commands[_cmd].Invoke(new CommandArgs
                            {
                                Client = this,
                                User = user,
                                Message = msg,
                                Args = arr,
                                Parsed = pars,
                            });
                        }
                    }
                    else
                    {
                        OnMessage?.Invoke(this, new HCMessageEventArgs
                        {
                            Client = this,
                            Message = msg,
                            User = user
                        });
                    }
                }
                else if (cmd == "info")
                {
                    Message msg = new Message
                    {
                        Client = this,
                        Time = (long)data["time"],
                        Text = (string)data["text"],
                        Type = MessageTypes.Info,
                    };

                    OnMessage?.Invoke(this, new HCMessageEventArgs
                    {
                        Client = this,
                        Message = msg,
                        User = new User
                        {
                            Client = this,
                            Role = UserRoles.Server
                        }
                    });
                }
                else if (cmd == "warn")
                {
                    Message msg = new Message
                    {
                        Client = this,
                        Time = (long)data["time"],
                        Text = (string)data["text"],
                        Type = MessageTypes.Warn,
                    };

                    OnMessage?.Invoke(this, new HCMessageEventArgs
                    {
                        Client = this,
                        Message = msg,
                        User = new User
                        {
                            Client = this,
                            Role = UserRoles.Server
                        }
                    });
                }
                else if (cmd == "onlineSet")
                {
                    JArray nicks = (JArray)data["nicks"];

                    foreach (JToken nick in nicks)
                    {
                        onlineUsers.Add(new User
                        {
                            Client = this,
                            Nick = nick.ToString()
                        });
                    }

                    OnOnlineSet?.Invoke(this, new HCOnlineSetEventArgs
                    {
                        Client = this,
                        Users = onlineUsers.ToArray(),
                    });
                }
                else if (cmd == "onlineAdd")
                {
                    User user = new User
                    {
                        Client = this,
                        Nick = (string)data["nick"]
                    };

                    onlineUsers.Add(user);

                    OnOnlineAdd?.Invoke(this, new HCOnlineAddEventArgs
                    {
                        Client = this,
                        User = user,
                    });
                }
                else if (cmd == "onlineRemove")
                {
                    string nick = (string)data["nick"];

                    for (int i = 0; i < this.onlineUsers.Count; i++)
                    {
                        User user = this.onlineUsers[i];

                        if (user.Nick == nick)
                        {
                            onlineUsers.RemoveAt(i);

                            OnOnlineRemove?.Invoke(this, new HCOnlineRemoveEventArgs
                            {
                                Client = this,
                                User = user,
                            });

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // ...
            }
        }

        private void ws_OnError(object sender, ErrorEventArgs e)
        {
            OnError?.Invoke(this, e);
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
        public HackClient Client;
        public string Nick;
        public string Trip;
        public UserRoles Role = UserRoles.User;

        public void Say(string text)
        {
            Client.Say($"@{(Nick != null ? Nick : "*")} {text}");
        }

        public override string ToString()
        {
            if (Nick == null)
                return "*";

            if (Trip != null)
                return Nick + "#" + Trip;

            return Nick;
        }
    }

    public class Message
    {
        public HackClient Client;
        public string Text;
        public MessageTypes Type = MessageTypes.Chat;
        public long Time;
    }

    public class CommandArgs
    {
        public HackClient Client;
        public User User;
        public Message Message;
        public string[] Args;
        public string[] Parsed;
    }
}