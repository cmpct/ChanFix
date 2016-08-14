using Meebey.SmartIrc4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChanFix
{
    public class User
    {
        public string Nick { get; set; }
        public string Ident { get; set; }
        public string Host { get; set; }
    }

    class Program
    {
        public static IrcClient irc = new IrcClient();
        // Key: Channel, Values: List of channel ops
        public static Dictionary<string, List<User>> operMap =
                  new Dictionary<string, List<User>>();
        // Key: Channel, Value: User trying to enroll
        public static Dictionary<string, string> tryToEnroll =
                  new Dictionary<string, string>();

        public static void Main(string[] args)
        {
            irc.OnRawMessage += OnRawMessage;
            irc.OnQueryMessage += OnQueryMessage;
            irc.OnChannelActiveSynced += OnChannelSync;

            irc.ActiveChannelSyncing = true;
            irc.SupportNonRfc = true;
            irc.Connect("bretagne.imaginarycode.com", 6667);
            irc.Login("ChanFix", "Channel op repair services");
            irc.RfcOper("calvin", "hacktheplanet");

            irc.Listen();
        }

        public static void ServiceOp(string channel, string user)
        {
            Debug.WriteLine(string.Format("Promoting {1} in {0}", channel, user), "Action");
            irc.WriteLine(string.Format("SAMODE {0} +o {1}", channel, user));
        }

        public static void Notice(string dest, string msg)
        {
            irc.SendMessage(SendType.Notice, dest, msg);
        }

        public static void OnQueryMessage(object sender, IrcEventArgs e)
        {
            var cmds = e.Data.Message.Split(' ');

            var cmd = cmds[0];
            var param = cmds[1];

            switch (cmd?.ToLower())
            {
                case "enroll":
                    if (!string.IsNullOrWhiteSpace(param))
                    {
                        tryToEnroll.Add(param, e.Data.Nick);
                        EnrollChannel(param);
                    }
                    else
                        Notice(e.Data.Nick, "No channel given.");
                    break;
                case "fix":
                    if (!string.IsNullOrWhiteSpace(param))
                        FixChannel(param);
                    else
                        Notice(e.Data.Nick, "No channel given.");
                    break;
                case "status":
                    if (!string.IsNullOrWhiteSpace(param))
                        if (operMap.ContainsKey(param))
                            if (operMap[param].Count > 0)
                                foreach (var u in operMap[param])
                                    Notice(e.Data.Nick, string.Format("{0} op: {1}!{2}@{3}",
                                        param, u.Nick, u.Ident, u.Host));
                            else
                                Notice(e.Data.Nick, "No ops were enrolled for this channel.");
                        else
                            Notice(e.Data.Nick, "The channel is unenrolled.");
                    else
                        Notice(e.Data.Nick, "No channel given.");
                    break;
                case "help":
                    Notice(e.Data.Nick, "ChanFix allows channel operators to easily restore their rights without IRC operator intervention.");
                    Notice(e.Data.Nick, "Recognized commands:");
                    Notice(e.Data.Nick, "enroll #channel: Enrolls a channel for use in ChanFix.");
                    Notice(e.Data.Nick, "fix #channel: Restores op status to everyone enrolled.");
                    Notice(e.Data.Nick, "status #channel: Lists the operators of a channel, or describes the state if there are none.");
                    break;
                default:
                    Notice(e.Data.Nick, "Unrecognized command. (tried help?)");
                    break;
            }
        }

        public static void OnRawMessage(object sender, IrcEventArgs e)
        {
            Debug.WriteLine(e.Data.RawMessage, "Raw");
        }

        public static void EnrollChannel(string channel)
        {
            irc.RfcJoin(channel);
        }

        // This is invoked in joins with Enroll
        public static void OnChannelSync(object sender, IrcEventArgs e)
        {
            Debug.WriteLine(string.Format("Enrolling {0}", e.Data.Channel), "Action");
            var c = irc.GetChannel(e.Data.Channel);

            string enroller = tryToEnroll[e.Data.Channel];
            ChannelUser enrollerAsUser = ((ChannelUser)c.Users[enroller]);

            if (enrollerAsUser.IsOp || enrollerAsUser.IsIrcOp)
            {
                // wipe/init the channel enrollment
                operMap[e.Data.Channel] = new List<User>();

                // it's not generic, specify type manually
                foreach (System.Collections.DictionaryEntry o in c.Ops)
                {
                    var u = (ChannelUser)o.Value;

                    operMap[e.Data.Channel].Add(new User()
                    {
                        Nick = u.Nick,
                        Host = u.Host,
                        Ident = u.Ident
                    });
                }

                irc.RfcPart(e.Data.Channel, "Channel enrolled into ChanFix.");
                Debug.WriteLine(string.Format("Enrolled {0}", e.Data.Channel), "Action");
            }
            else
            {
                irc.RfcPart(e.Data.Channel, string.Format("Channel couldn't be enrolled into ChanFix. ({0} wasn't op)", enroller));
                Debug.WriteLine(string.Format("Can't enroll {0} ({1} wasn't op)", e.Data.Channel, enroller), "Action");
            }
            tryToEnroll.Remove(e.Data.Channel);
        }

        public static void FixChannel(string channel)
        {
            Debug.WriteLine(string.Format("Fixing {0}", channel), "Action");
            if (!string.IsNullOrWhiteSpace(channel))
            {
                foreach (var u in operMap[channel])
                {
                    var iu = irc.GetIrcUser(u.Nick);
                    if (iu.Ident == u.Ident && iu.Host == iu.Host)
                    {
                        ServiceOp(channel, iu.Nick);
                    }
                }
            }
        }
    }
}
