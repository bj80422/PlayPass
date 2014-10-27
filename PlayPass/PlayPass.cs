using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using PlaySharp;

namespace PlayPass
{

    class PlayPass
    {
        string ServerHost = PlayOnConstants.DefaultHost;
        int ServerPort = PlayOnConstants.DefaultPort;
        string MediaStorageLocation = "";
        string MediaFileExt = "";
        public bool QueueMode = false;
        public bool VerboseMode = false;
        private TimeSpan _MaxRunTime = TimeSpan.FromSeconds(0);
        private TimeSpan _QueuedRunTime = TimeSpan.FromSeconds(0);

        /// <summary>
        /// Loads the PlayOn settings from the local computer's registry.
        /// </summary>
        void LoadPlayOnSettings()
        {
            MediaStorageLocation = PlayOnSettings.GetMediaStorageLocation();
            if (MediaStorageLocation == "")
                throw new Exception("Unable to find PlayLater's Media Storage Location");
            MediaFileExt = PlayOnSettings.GetPlayLaterVideoFormat();
        }

        /// <summary>
        /// Processes the config file by loading extra settings and then executing the ProcessPass procedure on each pass node.
        /// </summary>
        /// <param name="FileName"></param>
        public void ProcessConfigFile(string FileName)
        {
            LoadPlayOnSettings();
            try
            {
                XmlDocument Config = new XmlDocument();
                Config.Load(FileName);
                XmlNode SettingsNode = Config.SelectSingleNode("playpass/settings");
                if (SettingsNode != null)
                {
                    ServerHost = PlaySharp.Util.GetNodeAttributeValue(SettingsNode, "server", ServerHost);
                    ServerPort = int.Parse(PlaySharp.Util.GetNodeAttributeValue(SettingsNode, "port", ServerPort.ToString()));
                }

                PlayOn PlayOn = new PlayOn(ServerHost, ServerPort);

                XmlNode PassesNode = Config.SelectSingleNode("playpass/passes");
                if (PassesNode == null)
                    throw new Exception("A passes node was found in the config file");

                _MaxRunTime = TimeSpan.Parse(PlaySharp.Util.GetNodeAttributeValue(PassesNode, "max", _MaxRunTime.ToString(@"hh\:mm\:ss")));
                WriteLog("Max Run Time: " + _MaxRunTime);

                foreach (XmlNode PassNode in PassesNode.SelectNodes("pass"))
                    ProcessPass(PlayOn, PassNode);
            }
            catch (Exception ex)
            {
                WriteLog("Error processing config file: " + ex.Message.ToString());
            }
        }

        /// <summary>
        /// Executes the search and queue function on a pass node in the config file.
        /// </summary>
        /// <param name="PassNode">A pass node from the config file.</param>
        void ProcessPass(PlayOn PlayOn, XmlNode PassNode)
        {
            PlayOnItem CurrItem = PlayOn.GetCatalog();
            if (Util.GetNodeAttributeValue(PassNode, "enabled", "0") == "1")
            {
                WriteLog("=== Processing {0} ===", Util.GetNodeAttributeValue(PassNode, "description"));
                try
                {
                    foreach (XmlNode Node in PassNode.ChildNodes)
                    {
                        string MatchPattern = Util.GetNodeAttributeValue(Node, "name");
                        string ExcludePattern = Util.GetNodeAttributeValue(Node, "exclude");
                        bool FoundItem = false;
                        if (!(CurrItem is PlayOnFolder))
                            continue;
                        if (Node.Name == "scan")
                        {
                            WriteLog("   SEARCH: \"{0}\"", MatchPattern);
                            foreach (PlayOnItem ChildItem in ((PlayOnFolder)CurrItem).Items)
                            {
                                if (ChildItem is PlayOnFolder)
                                {
                                    if (VerboseMode)
                                        WriteLog("  COMPARE: \"{0}\"", ChildItem.Name);
                                    if (Util.MatchesPattern(ChildItem.Name, ExcludePattern))
                                    {
                                        WriteLog("  EXCLUDE: \"{0}\"", ChildItem.Name);
                                    }
                                    else if (Util.MatchesPattern(ChildItem.Name, MatchPattern))
                                    {
                                        if (VerboseMode)
                                            WriteLog("    MATCH: " + ChildItem.Name);
                                        FoundItem = true;
                                        CurrItem = ChildItem;
                                        break;
                                    }
                                }
                            }
                            if (!FoundItem)
                                WriteLog(" NO MATCH: \"{0}\"", MatchPattern);
                        }
                        else if (Node.Name == "queue")
                        {
                            WriteLog("   SEARCH: \"{0}\"", MatchPattern);
                            foreach (PlayOnItem ChildItem in ((PlayOnFolder)CurrItem).Items)
                            {
                                if (ChildItem is PlayOnVideo)
                                {
                                    if (VerboseMode)
                                        WriteLog("  COMPARE: \"{0}\"", ChildItem.Name);
                                    if (Util.MatchesPattern(ChildItem.Name, ExcludePattern))
                                    {
                                        WriteLog("  EXCLUDE: \"{0}\"", ChildItem.Name);
                                    }
                                    else if (Util.MatchesPattern(ChildItem.Name, MatchPattern))
                                    {
                                        if (VerboseMode)
                                            WriteLog("    MATCH: {0}", ChildItem.Name);
                                        QueueMedia((PlayOnVideo)ChildItem);
                                        FoundItem = true;
                                    }
                                }
                            }
                            if (!FoundItem)
                                WriteLog(" NO MATCH: \"{0}\"", MatchPattern);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Error processing pass: " + ex.Message.ToString());
                }
            }
        }

        /// <summary>
        /// Checks the local file system to see if the item has already been recorded.  If not, it will queue the video for record in PlayLater.
        /// </summary>
        void QueueMedia(PlayOnVideo Item)
        {
            bool Success = false;
            string Message = "";

            WriteOutput("  QUEUING: " + Item.Name + " (" + Item.RunTime + ")");
            string FileName = String.Format("{0} - {1}{2}", Item.Series, Item.MediaTitle, MediaFileExt);
            Regex re = new Regex("[<>:\"/\\|?*]");
            FileName = re.Replace(FileName, "_").TrimStart(' ','-');
            if (File.Exists(Path.Combine(MediaStorageLocation, FileName)))
                Message = String.Format("Video already recorded to {0}.", Path.Combine(MediaStorageLocation, FileName));
            else if (_MaxRunTime.TotalSeconds == 0 || _QueuedRunTime.Add(Item.RunTime) < _MaxRunTime)
            {
                if (!QueueMode)
                {
                    Success = true;
                    Message = "Run in queue mode to record";
                    _QueuedRunTime = _QueuedRunTime.Add(Item.RunTime);
                }
                else
                {
                    try
                    {
                        QueueVideoResult QueueResult = Item.AddToPlayLaterQueue();
                        if (QueueResult == QueueVideoResult.PlayLaterNotFound)
                            Message = "PlayLater queue link not found (Is PlayLater running?)";
                        else if (QueueResult == QueueVideoResult.AlreadyInQueue)
                            Message = "Media item is already in the queue";
                        if (QueueResult == QueueVideoResult.Success)
                        {
                            Success = true;
                            _QueuedRunTime = _QueuedRunTime.Add(Item.RunTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        Message = ex.Message.ToString();
                    }
                }
            } else
                Message = "Exceeds total runtime";
            WriteLog(" {0}{1}",(Success ? "[Success" : "[Skipped"), (Message == "" ? "" : ": " + Message) + "]");
        }

        /// <summary>
        /// Writes a log message to the console and to the debug area.
        /// </summary>
        void WriteLog(string Message)
        {
            Console.WriteLine(Message);
            Debug.WriteLine(Message);
        }

        /// <summary>
        /// Writes a log message to the console and to the debug area.
        /// </summary>
        void WriteLog(string Message, params object[] args)
        {
            Message = String.Format(Message, args);
            Console.WriteLine(Message);
            Debug.WriteLine(Message);
        }

        /// <summary>
        /// Writes a log message to the console and to the debug area without
        /// newline.
        /// </summary>
        void WriteOutput(string Message)
        {
            Console.Write(Message);
            Debug.Write(Message);
        }

        /// <summary>
        /// Writes a log message to the console and to the debug area without
        /// newline.
        /// </summary>
        void WriteOutput(string Message, params object[] args)
        {
            Message = String.Format(Message, args);
            Console.Write(Message);
            Debug.Write(Message);
        }
    }

}
